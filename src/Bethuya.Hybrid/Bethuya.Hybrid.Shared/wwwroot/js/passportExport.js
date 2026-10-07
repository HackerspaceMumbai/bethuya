window.BethuyaPassport = {
    downloadBase64: (fileName, contentType, base64) => {
        const bytes = Uint8Array.from(atob(base64), character => character.charCodeAt(0));
        const url = URL.createObjectURL(new Blob([bytes], { type: contentType }));
        const anchor = document.createElement("a");
        anchor.href = url;
        anchor.download = fileName;
        document.body.appendChild(anchor);
        anchor.click();
        anchor.remove();
        URL.revokeObjectURL(url);
    }
};
