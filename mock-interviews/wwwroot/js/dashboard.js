(() => {
  const timers = document.querySelectorAll("[data-dashboard-timer]");

  const updateTimers = () => {
    const now = Date.now();
    timers.forEach((timer) => {
      const startedAt = Date.parse(timer.dateTime);
      if (Number.isNaN(startedAt)) return;

      const elapsedSeconds = Math.max(0, Math.floor((now - startedAt) / 1000));
      const hours = Math.floor(elapsedSeconds / 3600);
      const minutes = Math.floor((elapsedSeconds % 3600) / 60);
      const seconds = elapsedSeconds % 60;
      timer.textContent = [hours, minutes, seconds]
        .map((value) => String(value).padStart(2, "0"))
        .join(":");
      timer.classList.toggle("text-negative", elapsedSeconds >= 1800);
      timer.classList.toggle("font-semibold", elapsedSeconds >= 1800);
    });
  };

  updateTimers();
  if (timers.length > 0) window.setInterval(updateTimers, 1000);

  document.querySelectorAll("form[data-complete-interview]").forEach((form) => {
    form.addEventListener("submit", async (event) => {
      event.preventDefault();
      const button = form.querySelector('button[type="submit"]');
      if (!button) return;

      button.disabled = true;
      button.textContent = "Updating…";

      try {
        const response = await fetch(form.action, {
          method: "POST",
          body: new FormData(form),
          credentials: "same-origin",
          headers: { "X-Requested-With": "XMLHttpRequest" },
        });
        if (!response.ok) throw new Error(`Request failed with ${response.status}`);
        window.location.reload();
      } catch (error) {
        const feedback = document.querySelector("[data-dashboard-feedback]");
        if (feedback) {
          feedback.classList.remove("hidden");
          feedback.innerHTML =
            '<div class="rounded-card border border-line border-l-4 border-l-negative bg-surface p-4 text-sm text-ink shadow-sm" role="alert">The interview could not be marked done. Please try again.</div>';
        }
        button.disabled = false;
        button.textContent = "Mark done";
        console.error(error);
      }
    });
  });
})();
