(function () {
  const selectedFileBySelector = new Map();
  const initializedInputs = new WeakSet();

  function getInput(inputSelector) {
    const input = document.querySelector(inputSelector);
    if (!input || !(input instanceof HTMLInputElement)) {
      return null;
    }

    return input;
  }

  function initDirectImageUpload(inputSelector) {
    const input = getInput(inputSelector);
    if (!input) {
      return;
    }

    if (initializedInputs.has(input)) {
      return;
    }

    const updateSelectedFile = () => {
      if (input.files && input.files.length > 0) {
        selectedFileBySelector.set(inputSelector, input.files[0]);
      } else {
        selectedFileBySelector.delete(inputSelector);
      }
    };

    input.addEventListener("change", updateSelectedFile);
    updateSelectedFile();
    initializedInputs.add(input);
  }

  function consumeSelectedFile(inputSelector) {
    const cached = selectedFileBySelector.get(inputSelector);
    if (cached) {
      return cached;
    }

    const input = getInput(inputSelector);
    if (!input || !input.files || input.files.length === 0) {
      return null;
    }

    return input.files[0];
  }

  function clearSelectedFile(inputSelector) {
    selectedFileBySelector.delete(inputSelector);
  }

  async function uploadDirectImage(inputSelector, uploadSession) {
    const file = consumeSelectedFile(inputSelector);
    if (!file) {
      throw new Error("No file is currently selected for upload.");
    }

    const formData = new FormData();
    formData.append("file", file);
    formData.append("api_key", uploadSession.apiKey);
    formData.append("timestamp", String(uploadSession.timestamp));
    formData.append("signature", uploadSession.signature);
    formData.append("public_id", uploadSession.publicId);
    formData.append("allowed_formats", uploadSession.allowedFormats);

    const response = await fetch(uploadSession.uploadUrl, {
      method: "POST",
      body: formData
    });

    const payload = await response.json().catch(() => null);
    if (!response.ok) {
      throw new Error(payload?.error?.message ?? "Cloudinary rejected the upload.");
    }

    clearSelectedFile(inputSelector);

    return {
      secureUrl: payload?.secure_url ?? "",
      publicId: payload?.public_id ?? uploadSession.publicId
    };
  }

  window.bethuyaCloudinaryUpload = {
    initDirectImageUpload,
    clearSelectedFile,
    uploadDirectImage
  };
})();
