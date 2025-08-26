window.slotCharts = window.slotCharts || {};

window.renderChart = (canvasId, chartData) => {
    const ctx = document.getElementById(canvasId).getContext('2d');
    if (window.slotCharts[canvasId] && typeof window.slotCharts[canvasId].destroy === 'function') {
        window.slotCharts[canvasId].destroy();
    }

    // Add tooltip callback if vessels or rails data is provided
    if (chartData.vessels || chartData.rails) {
        if (!chartData.options) chartData.options = {};
        if (!chartData.options.plugins) chartData.options.plugins = {};
        if (!chartData.options.plugins.tooltip) chartData.options.plugins.tooltip = {};

        console.log('Adding custom tooltip callback for vessels and/or rails');
        // console.log('Vessels data:', chartData.vessels);
        // console.log('Rails data:', chartData.rails);

        chartData.options.plugins.tooltip.callbacks = {
            label: function(context) {
                try {
                    const label = context.dataset.label || '';
                    const value = context.formattedValue || '';

                    if (label.includes('Vessel') && chartData.vessels) {
                        const hourIndex = context.dataIndex;
                        const hourKey = chartData.data.labels[hourIndex];
                        const vessels = chartData.vessels[hourKey] || [];
                        return label + ': ' + value + ' (' + vessels.join(',') + ')';
                    }
                    if (label.includes('Rail') && chartData.rails) {
                        const hourIndex = context.dataIndex;
                        const hourKey = chartData.data.labels[hourIndex];
                        const rails = chartData.rails[hourKey] || [];
                        return label + ': ' + value + ' (' + rails.join(',') + ')';
                    }
                    return label + ': ' + value;
                } catch (error) {
                    console.error('Error in tooltip callback:', error);
                    const label = context.dataset.label || '';
                    const value = context.formattedValue || '';
                    return label + ': ' + value;
                }
            }
        };


    }

    // Ensure zoom plugin is enabled
    if (!chartData.options) chartData.options = {};
    if (!chartData.options.plugins) chartData.options.plugins = {};
    chartData.options.plugins.zoom = {
        zoom: {
            wheel: {
                enabled: true
            },
            pinch: {
                enabled: true
            },
            mode: 'xy'
        },
        pan: {
            enabled: true,
            mode: 'xy'
        }
    };

    window.slotCharts[canvasId] = new Chart(ctx, chartData);
};

// Reset function for all charts
window.resetAllZooms = () => {
    for (const id in window.slotCharts) {
        if (window.slotCharts[id] && window.slotCharts[id].resetZoom) {
            window.slotCharts[id].resetZoom();
        }
    }
};

window.destroyChart = (canvasId) => {
    if (window.slotCharts[canvasId] && typeof window.slotCharts[canvasId].destroy === 'function') {
        window.slotCharts[canvasId].destroy();
        delete window.slotCharts[canvasId];
    }
};