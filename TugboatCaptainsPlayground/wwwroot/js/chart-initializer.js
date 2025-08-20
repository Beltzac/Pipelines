window.slotCharts = window.slotCharts || {};

window.renderChart = (canvasId, chartData) => {
    const ctx = document.getElementById(canvasId).getContext('2d');
    if (window.slotCharts[canvasId] && typeof window.slotCharts[canvasId].destroy === 'function') {
        window.slotCharts[canvasId].destroy();
    }
    window.slotCharts[canvasId] = new Chart(ctx, chartData);
};

window.destroyChart = (canvasId) => {
    if (window.slotCharts[canvasId] && typeof window.slotCharts[canvasId].destroy === 'function') {
        window.slotCharts[canvasId].destroy();
        delete window.slotCharts[canvasId];
    }
};