(() => {
  const search = document.querySelector("[data-people-search]");
  const rows = Array.from(document.querySelectorAll("[data-people-row]"));
  const emptyMessage = document.querySelector("[data-people-search-empty]");
  const resultCount = document.querySelector("[data-people-result-count]");

  if (!search || rows.length === 0) {
    return;
  }

  const filterRows = () => {
    const query = search.value.trim().toLocaleLowerCase();
    let visibleCount = 0;

    rows.forEach((row) => {
      const matches = row.getAttribute("data-search-text").toLocaleLowerCase().includes(query);
      row.hidden = !matches;
      visibleCount += matches ? 1 : 0;
    });

    emptyMessage?.classList.toggle("hidden", visibleCount !== 0);
    if (resultCount) {
      resultCount.textContent = `${visibleCount} ${visibleCount === 1 ? "person" : "people"} shown`;
    }
  };

  search.addEventListener("input", filterRows);
})();
