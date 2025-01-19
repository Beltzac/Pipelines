window.addKeyboardShortcuts = (dotNetHelper) => {
    document.addEventListener('keydown', (e) => {
        if (e.key === 'Enter' && e.ctrlKey) {
            dotNetHelper.invokeMethodAsync('HandleKeyPress', 'Enter');
        } else if (e.key === 'Escape') {
            dotNetHelper.invokeMethodAsync('HandleKeyPress', 'Escape');
        }
    });
};
