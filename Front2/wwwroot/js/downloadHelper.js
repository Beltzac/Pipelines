let dotNetReference;

function setDotNetReference(reference) {
    dotNetReference = reference;
}

function downloadFile(fileName, base64Content) {
    const link = document.createElement('a');
    link.download = fileName;
    link.href = 'data:text/plain;base64,' + base64Content;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
}