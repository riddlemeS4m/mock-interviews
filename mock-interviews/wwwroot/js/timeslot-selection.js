document.querySelectorAll("[data-timeslot-selection]").forEach((selection) => {
  if (selection.dataset.timeslotSelectionInitialized === "true") return;
  selection.dataset.timeslotSelectionInitialized = "true";

  selection.querySelectorAll("[data-timeslot-day]").forEach((day) => {
    const button = day.querySelector("[data-timeslot-select-all]");
    const options = [...day.querySelectorAll("[data-timeslot-option]")];
    if (!button || options.length === 0) return;

    const updateButton = () => {
      const allSelected = options.every((option) => option.checked);
      button.setAttribute("aria-pressed", String(allSelected));
      button.textContent = allSelected ? "Clear all" : "Select all";
    };

    button.addEventListener("click", () => {
      const nextValue = !options.every((option) => option.checked);
      options.forEach((option) => { option.checked = nextValue; });
      updateButton();
    });
    options.forEach((option) => option.addEventListener("change", updateButton));
    updateButton();
  });
});
