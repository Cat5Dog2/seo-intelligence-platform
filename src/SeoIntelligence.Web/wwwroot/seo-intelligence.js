window.seoIntelligence = {
    readTextFile: async function (inputId) {
        const input = document.getElementById(inputId);
        if (!input || !input.files || input.files.length === 0) {
            return "";
        }

        return await input.files[0].text();
    }
};
