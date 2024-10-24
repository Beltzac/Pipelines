let dotNetReference;

function setDotNetReference(reference) {
    dotNetReference = reference;
}

function downloadFile(filename, contentType, base64Content) {
    const link = document.createElement('a');
    link.href = `data:${contentType};base64,${base64Content}`;
    link.download = filename;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
}
