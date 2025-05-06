/* Manages theme stylesheets in <head> */
window.themeManager = {
    load: href => {
        // Remove any existing theme links first
        document.querySelectorAll('link[data-theme]').forEach(link => link.remove());

        if (href) {
            const link = document.createElement('link');
            link.dataset.theme = 'custom';
            link.rel = 'stylesheet';
            link.href = href;
            document.head.appendChild(link);
        }
    },
    unload: () => {
        document.querySelectorAll('link[data-theme]').forEach(link => link.remove());
    }
};