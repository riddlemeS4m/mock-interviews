(() => {
  const openers = document.querySelectorAll("[data-dialog-target]");
  const dialogs = document.querySelectorAll("dialog[data-dialog]");
  let lastOpener = null;

  const populateDialog = (dialog, opener) => {
    dialog.querySelectorAll("[data-dialog-field]").forEach((field) => {
      const key = field.getAttribute("data-dialog-field");
      const value = opener.getAttribute(`data-dialog-value-${key}`);

      if (value === null) {
        return;
      }

      if (field instanceof HTMLInputElement && field.type === "checkbox") {
        field.checked = value === "true";
      } else if (field instanceof HTMLInputElement || field instanceof HTMLTextAreaElement || field instanceof HTMLSelectElement) {
        field.value = value;
      } else {
        field.textContent = value;
      }
    });
  };

  const openDialog = (dialog, opener = null) => {
    if (typeof dialog.showModal !== "function") {
      return false;
    }

    if (opener) {
      populateDialog(dialog, opener);
      lastOpener = opener;
    }

    if (!dialog.open) {
      dialog.showModal();
    }

    dialog.querySelector(".input-validation-error, [data-dialog-initial-focus]")?.focus();
    return true;
  };

  openers.forEach((opener) => {
    opener.addEventListener("click", (event) => {
      const dialog = document.getElementById(opener.getAttribute("data-dialog-target"));
      if (!(dialog instanceof HTMLDialogElement) || !openDialog(dialog, opener)) {
        return;
      }

      event.preventDefault();
    });
  });

  dialogs.forEach((dialog) => {
    dialog.querySelectorAll("[data-dialog-close]").forEach((closer) => {
      closer.addEventListener("click", () => dialog.close());
    });

    dialog.addEventListener("click", (event) => {
      if (event.target === dialog) {
        dialog.close();
      }
    });

    dialog.addEventListener("close", () => {
      lastOpener?.focus();
      lastOpener = null;
    });

    if (dialog.getAttribute("data-dialog-auto-open") === "true") {
      openDialog(dialog);
    }
  });
})();
