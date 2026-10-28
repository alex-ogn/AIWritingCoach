// Small helpers called from Blazor via JS interop.
window.aiwc = {
    scrollIntoView: function (id) {
        const el = document.getElementById(id);
        if (el) {
            el.scrollIntoView({ behavior: "smooth", block: "center", inline: "nearest" });
        }
    }
};
