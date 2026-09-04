document.querySelectorAll("[data-interviewer-form]").forEach((form) => {
  const lunch = form.querySelector("[data-lunch-option]");
  const modes = form.querySelectorAll("[data-location-mode]");
  if (!lunch || modes.length === 0) return;
  const updateLunch = () => {
    const inPerson = [...modes].some((mode) => mode.checked && mode.value === "true");
    lunch.hidden = !inPerson;
    lunch.querySelector("input").disabled = !inPerson;
  };
  modes.forEach((mode) => mode.addEventListener("change", updateLunch));
  updateLunch();
});
