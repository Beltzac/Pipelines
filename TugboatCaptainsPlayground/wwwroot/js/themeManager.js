/* adds or removes <link id="90s-theme-link"> in <head> */
window.themeManager = {
    load: href => {
        if (!document.getElementById('90s-theme-link')) {
            const link = document.createElement('link');
            link.id = '90s-theme-link';
            link.rel = 'stylesheet';
            link.href = href;
            document.head.appendChild(link);
        }
    },
    unload: () => {
        const link = document.getElementById('90s-theme-link');
        if (link) link.remove();
    }
};