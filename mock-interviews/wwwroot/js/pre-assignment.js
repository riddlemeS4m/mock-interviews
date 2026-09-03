(() => {
  const filters = document.querySelector("[data-preassignment-date-filters]");
  if (!filters) {
    return;
  }

  const buttons = [...filters.querySelectorAll("[data-preassignment-date-filter]")];
  const groups = [...document.querySelectorAll("[data-preassignment-date]")];
  const selectDate = (date) => {
    buttons.forEach((button) => {
      const selected = button.dataset.preassignmentDateFilter === date;
      button.setAttribute("aria-pressed", selected.toString());
    });
    groups.forEach((group) => {
      group.hidden = date !== "all" && group.dataset.preassignmentDate !== date;
    });
  };

  buttons.forEach((button) => {
    button.addEventListener("click", () => selectDate(button.dataset.preassignmentDateFilter || "all"));
  });

  const firstDate = buttons.find((button) => button.dataset.preassignmentDateFilter !== "all");
  selectDate(firstDate?.dataset.preassignmentDateFilter || "all");
})();
