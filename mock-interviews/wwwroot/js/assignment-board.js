(() => {
  const status = document.querySelector("[data-assignment-board-status]");
  const refreshUrl = "/InterviewEvents/Board";
  let refreshTimer = null;
  let isRefreshing = false;
  let lastOpener = null;

  const announce = (message) => {
    if (status) {
      status.textContent = message;
    }
  };

  const bindDialogs = (root) => {
    root.querySelectorAll("[data-dialog-target]").forEach((opener) => {
      opener.addEventListener("click", (event) => {
        const dialog = document.getElementById(opener.getAttribute("data-dialog-target"));
        if (!(dialog instanceof HTMLDialogElement) || typeof dialog.showModal !== "function") {
          return;
        }

        event.preventDefault();
        lastOpener = opener;
        if (!dialog.open) {
          dialog.showModal();
        }
        dialog.querySelector("[data-dialog-initial-focus]")?.focus();
      });
    });

    root.querySelectorAll("dialog[data-dialog]").forEach((dialog) => {
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
    });
  };

  const bindCommands = (root) => {
    root.querySelectorAll("form[data-assignment-board-command]").forEach((form) => {
      form.addEventListener("submit", async (event) => {
        event.preventDefault();
        const submitter = event.submitter;
        if (submitter instanceof HTMLButtonElement) {
          submitter.disabled = true;
        }

        try {
          const response = await fetch(form.action, {
            method: "POST",
            body: new FormData(form),
            credentials: "same-origin",
            headers: { "X-Requested-With": "XMLHttpRequest" }
          });
          if (!response.ok) {
            announce("The board changed before this action could be completed. Refreshing current state.");
            await refreshBoard(false);
            return;
          }

          form.closest("dialog")?.close();
          await refreshBoard(false);
          announce("Assignment board updated.");
        } catch {
          announce("Unable to update the board. Your action may still have completed; refresh to confirm.");
        } finally {
          if (submitter instanceof HTMLButtonElement) {
            submitter.disabled = false;
          }
        }
      });
    });
  };

  const refreshBoard = async (announceCompletion = true) => {
    if (isRefreshing) {
      return;
    }

    const currentRegion = document.getElementById("assignment-board-region");
    if (!currentRegion) {
      return;
    }

    isRefreshing = true;
    const focusedInterview = document.activeElement?.closest?.("[data-interview-id]")?.getAttribute("data-interview-id");
    try {
      const response = await fetch(refreshUrl, {
        credentials: "same-origin",
        headers: { "X-Requested-With": "XMLHttpRequest" }
      });
      if (!response.ok) {
        throw new Error(`Refresh failed: ${response.status}`);
      }

      const template = document.createElement("template");
      template.innerHTML = await response.text();
      const replacement = template.content.querySelector("#assignment-board-region");
      if (!replacement) {
        throw new Error("Board refresh did not include the board region.");
      }

      currentRegion.replaceWith(replacement);
      bindDialogs(replacement);
      bindCommands(replacement);
      if (focusedInterview) {
        replacement.querySelector(`[data-interview-id="${CSS.escape(focusedInterview)}"] button, [data-interview-id="${CSS.escape(focusedInterview)}"] a`)?.focus();
      }
      if (announceCompletion) {
        announce("Assignment board refreshed.");
      }
    } catch {
      announce("The board could not be refreshed. Try again shortly.");
    } finally {
      isRefreshing = false;
    }
  };

  const scheduleRefresh = () => {
    window.clearTimeout(refreshTimer);
    refreshTimer = window.setTimeout(() => refreshBoard(), 150);
  };

  const initialRegion = document.getElementById("assignment-board-region");
  if (!initialRegion) {
    return;
  }

  bindCommands(initialRegion);

  if (!window.signalR) {
    announce("Live updates are unavailable. You can continue using the board and refresh manually.");
    return;
  }

  const connection = new window.signalR.HubConnectionBuilder()
    .withUrl("/interviewhub")
    .withAutomaticReconnect()
    .build();

  connection.on("BoardChanged", scheduleRefresh);
  connection.onreconnecting(() => announce("Live updates reconnecting. You can continue using the board."));
  connection.onreconnected(() => {
    announce("Live updates reconnected.");
    scheduleRefresh();
  });
  connection.onclose(() => announce("Live updates disconnected. Refresh the board to see changes from other staff."));
  connection.start().catch(() => announce("Live updates are unavailable. You can continue using the board and refresh manually."));
})();
