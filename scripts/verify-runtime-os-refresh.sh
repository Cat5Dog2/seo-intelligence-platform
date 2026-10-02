#!/usr/bin/env bash
# Check the actual Bake/Compose build plans and keep the refresh argument out of .NET stages.
# No images are built and no containers are started.
set -euo pipefail

cd "$(dirname "$0")/.."

if command -v python3 > /dev/null 2>&1 && python3 -c "" > /dev/null 2>&1; then
  python_bin=python3
else
  python_bin=python
fi

"$python_bin" - <<'PY'
import json
import os
from pathlib import Path
import re
import shlex
import subprocess

targets = {"api", "web", "worker", "migrate"}
argument = "RUNTIME_OS_REFRESH"

dockerfile = Path("Dockerfile").read_text(encoding="utf-8")
# Dockerfile comments are ignored even between continuation lines. Join each instruction before
# inspecting its stage and shell commands so comments, labels and printed text cannot satisfy it.
instructions = []
parts = []
for line in dockerfile.splitlines():
    line = line.strip()
    if not line or line.startswith("#"):
        continue
    continued = line.endswith("\\")
    parts.append(line[:-1] if continued else line)
    if not continued:
        kind, value = " ".join(parts).split(None, 1)
        instructions.append((kind.upper(), value))
        parts = []
assert not parts, "unterminated Dockerfile instruction"

stage = None
declarations = []
runtime = []
for kind, value in instructions:
    if kind == "FROM":
        match = re.search(r"\bAS\s+(\S+)$", value, re.IGNORECASE)
        stage = match[1] if match else None
    if kind == "ARG" and value.split("=", 1)[0] == argument:
        declarations.append(stage)
    if stage == "runtime-base":
        runtime.append((kind, value))
assert declarations == ["runtime-base"], "OS refresh ARG must be scoped only to runtime-base"
argument_index = next(i for i, (kind, value) in enumerate(runtime)
                      if kind == "ARG" and value.split("=", 1)[0] == argument)
run_index = next(i for i, (kind, _) in enumerate(runtime) if kind == "RUN")
assert argument_index < run_index, "declare the refresh ARG before the OS update RUN"

commands = []
for kind, value in runtime:
    if kind != "RUN":
        continue
    lexer = shlex.shlex(value, posix=True, punctuation_chars=";&|")
    lexer.whitespace_split = True
    command = []
    for token in [*lexer, ";"]:
        if token in {";", "&&", "||", "|", "&"}:
            while command and re.match(r"^[A-Za-z_]\w*=", command[0]):
                command = command[1:]
            commands.append(command)
            command = []
        else:
            command.append(token)
updates = [command for command in commands if command[:2] == ["apt-get", "update"]]
assert updates and all("--error-on=any" in command[2:] for command in updates), "fail the OS update if any package index cannot be fetched"
assert any(command[:2] == ["apt-get", "upgrade"] and {"-y", "--no-install-recommends"} <= set(command[2:])
           for command in commands), "update all installed OS packages"

workflow = Path(".github/workflows/ci.yaml").read_text(encoding="utf-8")
bake_step = workflow.split("      - name: Build application containers\n", 1)[1].split("\n      #", 1)[0]
assert "files: compose.yaml" in bake_step, "verify the same Compose file CI builds"
assert "load: true" in bake_step, "CI must load the images it scans"
target_lines = re.search(r"          targets: \|\n((?:            \w+\n)+)", bake_step)
assert target_lines and set(target_lines[1].split()) == targets, "CI must build all four application targets"
overrides = re.findall(r"^            (\*\.[^\n]+)$", bake_step, re.MULTILINE)
assert any(f"*.args.{argument}=" in value for value in overrides), "CI must pass the OS refresh ARG"

environment = dict(os.environ, POSTGRES_PASSWORD="compose-validation-only",
                   API_SERVICE_KEY="compose-validation-only", CADDY_NETWORK_SUBNET="10.89.0.0/28",
                   APP_ENV_FILE=".env.production.app.example")

def render(command):
    result = subprocess.run(command, env=environment, text=True, encoding="utf-8",
                            capture_output=True, check=True)
    return json.loads(result.stdout)["target"]

seen_refreshes = set()
for run_id, attempt in ((100, 1), (101, 1), (100, 2)):
    resolved = [value.replace("${{ github.run_id }}", str(run_id))
                     .replace("${{ github.run_attempt }}", str(attempt)) for value in overrides]
    command = ["docker", "buildx", "bake", "-f", "compose.yaml", "--print"]
    for value in resolved:
        command += ["--set", value]
    plan = render(command + sorted(targets))
    refresh_values = {plan[name].get("args", {}).get(argument) for name in targets}
    assert len(refresh_values) == 1, "CI targets must share one OS refresh value"
    refresh = refresh_values.pop()
    assert refresh and "${{" not in refresh and refresh not in seen_refreshes, "CI runs and reruns must use distinct OS refresh values"
    for name in targets:
        assert plan[name]["target"] == name
        assert plan[name].get("cache-from"), f"{name}: retain the CI cache import"
        assert plan[name].get("cache-to"), f"{name}: retain the CI cache export"
        assert not plan[name].get("no-cache"), f"{name}: retain the .NET build cache"
    seen_refreshes.add(refresh)
print("PASS: CI renders a fresh shared OS refresh ARG on new runs and reruns and retains layer caching")

plan = render(["docker", "compose", "--project-name", "seo-intelligence-prod",
               "--env-file", ".env.production.example", "-f", "compose.yaml", "-f",
               "compose.production.yaml", "build", "--print", "--build-arg",
               f"{argument}=verify-compose-refresh", *sorted(targets)])
for name in targets:
    assert plan[name]["target"] == name
    assert plan[name]["args"][argument] == "verify-compose-refresh", f"{name}: missing Compose ARG"
    assert not plan[name].get("no-cache"), f"{name}: retain the .NET build cache"
print("PASS: VPS Compose passes the refresh ARG to all four targets without disabling caching")
print("PASS: the refresh ARG is declared only in runtime-base")
PY
