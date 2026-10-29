// Small helpers called from Blazor via JS interop.
window.aiwc = {
    scrollIntoView: function (id) {
        const el = document.getElementById(id);
        if (el) {
            el.scrollIntoView({ behavior: "smooth", block: "center", inline: "nearest" });
        }
    },

    // A `download` link is not intercepted by the Blazor router, so the page stays put.
    download: function (url) {
        const a = document.createElement("a");
        a.href = url;
        a.download = "";
        document.body.appendChild(a);
        a.click();
        a.remove();
    }
};
