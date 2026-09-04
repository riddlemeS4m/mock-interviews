(() => {
  const shell = document.querySelector("[data-shell-navigation]");

  if (!shell) {
    return;
  }

  const toggle = shell.querySelector("[data-navigation-toggle]");
  const closeButton = shell.querySelector("[data-navigation-close]");
  const panel = shell.querySelector("[data-navigation-panel]");
  const overlay = shell.querySelector("[data-navigation-overlay]");
  const collapseButton = shell.querySelector("[data-navigation-collapse]");
  const collapseIcon = shell.querySelector("[data-navigation-collapse-icon]");
  const disclosureSummaries = shell.querySelectorAll("[data-navigation-summary]");
  const desktopBreakpoint = window.matchMedia("(min-width: 64rem)");
  const storageKey = "mock-interviews.navigation-collapsed";

  const setOpen = (isOpen, restoreFocus = false) => {
    toggle?.setAttribute("aria-expanded", String(isOpen));
    panel?.classList.toggle("-translate-x-full", !isOpen);
    overlay?.classList.toggle("hidden", !isOpen);
    document.body.classList.toggle("overflow-hidden", isOpen);
    if (panel) {
      panel.inert = !desktopBreakpoint.matches && !isOpen;
    }

    if (isOpen) {
      closeButton?.focus();
    } else if (restoreFocus) {
      toggle?.focus();
    }
  };

  const setCollapsed = (isCollapsed, persist = true) => {
    if (isCollapsed) {
      document.documentElement.dataset.navigationCollapsed = "true";
    } else {
      delete document.documentElement.dataset.navigationCollapsed;
    }

    collapseButton?.setAttribute("aria-expanded", String(!isCollapsed));
    collapseButton?.setAttribute("aria-label", isCollapsed ? "Expand navigation" : "Collapse navigation");
    collapseButton?.setAttribute("title", isCollapsed ? "Expand navigation" : "Collapse navigation");
    collapseIcon?.classList.toggle("rotate-180", !isCollapsed);

    if (persist) {
      try {
        localStorage.setItem(storageKey, String(isCollapsed));
      } catch {
        // The navigation still works when browser storage is unavailable.
      }
    }
  };

  toggle?.addEventListener("click", () => {
    setOpen(toggle.getAttribute("aria-expanded") !== "true");
  });

  closeButton?.addEventListener("click", () => setOpen(false, true));
  overlay?.addEventListener("click", () => setOpen(false, true));
  collapseButton?.addEventListener("click", () => {
    setCollapsed(document.documentElement.dataset.navigationCollapsed !== "true");
  });

  disclosureSummaries.forEach((summary) => {
    summary.addEventListener("click", (event) => {
      if (!desktopBreakpoint.matches || document.documentElement.dataset.navigationCollapsed !== "true") {
        return;
      }

      event.preventDefault();
      setCollapsed(false);
      summary.closest("details").open = true;
      summary.focus();
    });
  });

  document.addEventListener("keydown", (event) => {
    if (event.key === "Escape" && toggle?.getAttribute("aria-expanded") === "true") {
      setOpen(false, true);
    }
  });

  desktopBreakpoint.addEventListener("change", () => setOpen(false));

  setCollapsed(document.documentElement.dataset.navigationCollapsed === "true", false);
  setOpen(false);
})();
