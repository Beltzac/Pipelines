window.slotCharts = window.slotCharts || {};

window.renderChart = (canvasId, chartData) => {
    const ctx = document.getElementById(canvasId).getContext('2d');
    if (window.slotCharts[canvasId] && typeof window.slotCharts[canvasId].destroy === 'function') {
        window.slotCharts[canvasId].destroy();
    }

    // Add vertical line plugin
    const verticalLinePlugin = {
        id: 'verticalLine',
        afterDraw: (chart) => {
            const pluginOptions = chart.config.options.plugins.verticalLine;
            if (pluginOptions && pluginOptions.index !== undefined && pluginOptions.index !== -1) {
                const { ctx, chartArea: { top, bottom, left, right } } = chart;
                const index = pluginOptions.index;
                
                if (index < 0 || !chart.data.labels || index >= chart.data.labels.length) return;

                // Get X position from the first dataset's meta data
                const meta = chart.getDatasetMeta(0);
                if (!meta || !meta.data || !meta.data[index]) return;
                
                const xPos = meta.data[index].x;
                if (isNaN(xPos) || xPos < left || xPos > right) return;

                const color = pluginOptions.color || 'red';
                const label = pluginOptions.label || '';

                ctx.save();
                ctx.strokeStyle = color;
                ctx.setLineDash([5, 5]);
                ctx.lineWidth = 2;
                ctx.beginPath();
                ctx.moveTo(xPos, top);
                ctx.lineTo(xPos, bottom);
                ctx.stroke();

                ctx.fillStyle = color;
                ctx.font = 'bold 12px sans-serif';
                ctx.textAlign = 'left';
                ctx.fillText(label, xPos + 5, top + 15);
                ctx.restore();
            }
        }
    };

    if (!chartData.plugins) chartData.plugins = [];
    // Check if already added to avoid duplicates
    if (!chartData.plugins.some(p => p.id === 'verticalLine')) {
        chartData.plugins.push(verticalLinePlugin);
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

    chartData.options.plugins.decimation = {
        enabled: true,
        algorithm: 'lttb'
    };

    // Create and store the chart instance

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