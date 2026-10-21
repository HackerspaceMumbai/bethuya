window.bethuyaEvidenceDrawer = {
  open(dialog) {
    if (!dialog || typeof dialog.showModal !== "function") {
      return;
    }

    if (!dialog.open) {
      dialog.showModal();
    }
  },
  close(dialog) {
    if (!dialog || typeof dialog.close !== "function") {
      return;
    }

    if (dialog.open) {
      dialog.close();
    }
  },
  focusSelector(selector) {
    if (!selector) {
      return;
    }

    const target = document.querySelector(selector);
    if (target && typeof target.focus === "function") {
      target.focus();
    }
  },
  closeAndRestoreFocus(dialog, selector) {
    this.close(dialog);
    this.focusSelector(selector);
  }
};
