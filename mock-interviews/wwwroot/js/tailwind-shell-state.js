(() => {
  try {
    if (localStorage.getItem("mock-interviews.navigation-collapsed") === "true") {
      document.documentElement.dataset.navigationCollapsed = "true";
    }
  } catch {
    // Use the expanded navigation when browser storage is unavailable.
  }
})();
