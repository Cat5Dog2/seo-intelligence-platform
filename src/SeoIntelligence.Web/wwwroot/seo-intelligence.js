window.seoIntelligence = {
    openDialog: function (id) {
        const dialog = document.getElementById(id);
        if (!dialog || dialog.open) return;
        dialog.showModal();
        // Native dialogs provide Escape handling, focus containment and focus return.
        const dismissBackdrop = event => {
            if (event.target !== dialog) return;
            const bounds = dialog.getBoundingClientRect();
            if (event.clientX < bounds.left || event.clientX > bounds.right ||
                event.clientY < bounds.top || event.clientY > bounds.bottom) dialog.close();
        };
        dialog.addEventListener("click", dismissBackdrop);
        dialog.addEventListener("close", () => dialog.removeEventListener("click", dismissBackdrop), { once: true });
    },
    closeDialog: function (id) {
        document.getElementById(id)?.close();
    },
    focusSection: function (id) {
        const heading = document.getElementById(id);
        if (!heading) return;
        heading.focus({ preventScroll: true });
        heading.scrollIntoView({ block: "start", behavior: "instant" });
    },
    readTextFile: async function (inputId) {
        const input = document.getElementById(inputId);
        if (!input || !input.files || input.files.length === 0) {
            return "";
        }

        return await input.files[0].text();
    }
};
