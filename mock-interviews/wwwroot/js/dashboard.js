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
})();
