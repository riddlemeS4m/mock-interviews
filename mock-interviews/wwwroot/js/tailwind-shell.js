(() => {
  const navigation = document.querySelector("[data-shell-navigation]");

  if (!navigation) {
    return;
  }

  const navigationToggle = navigation.querySelector("[data-navigation-toggle]");
  const navigationPanel = navigation.querySelector("[data-navigation-panel]");
  const menus = Array.from(navigation.querySelectorAll("[data-navigation-menu]"));

  const setMenuOpen = (menu, isOpen) => {
    const button = menu.querySelector("[data-menu-toggle]");
    const panel = menu.querySelector("[data-menu-panel]");

    button?.setAttribute("aria-expanded", String(isOpen));
    panel?.classList.toggle("hidden", !isOpen);
  };

  const closeMenus = (exceptMenu = null) => {
    menus.forEach((menu) => {
      if (menu !== exceptMenu) {
        setMenuOpen(menu, false);
      }
    });
  };

  navigationToggle?.addEventListener("click", () => {
    const isOpen = navigationToggle.getAttribute("aria-expanded") === "true";
    navigationToggle.setAttribute("aria-expanded", String(!isOpen));
    navigationPanel?.classList.toggle("hidden", isOpen);

    if (isOpen) {
      closeMenus();
    }
  });

  menus.forEach((menu) => {
    const button = menu.querySelector("[data-menu-toggle]");

    button?.addEventListener("click", () => {
      const isOpen = button.getAttribute("aria-expanded") === "true";
      closeMenus(menu);
      setMenuOpen(menu, !isOpen);
    });
  });

  document.addEventListener("click", (event) => {
    if (!navigation.contains(event.target)) {
      closeMenus();
    }
  });

  document.addEventListener("keydown", (event) => {
    if (event.key !== "Escape") {
      return;
    }

    const openMenu = menus.find(
      (menu) => menu.querySelector("[data-menu-toggle]")?.getAttribute("aria-expanded") === "true",
    );

    if (openMenu) {
      setMenuOpen(openMenu, false);
      openMenu.querySelector("[data-menu-toggle]")?.focus();
    }
  });

  const desktopBreakpoint = window.matchMedia("(min-width: 64rem)");
  desktopBreakpoint.addEventListener("change", (event) => {
    if (event.matches) {
      navigationPanel?.classList.add("hidden");
      navigationToggle?.setAttribute("aria-expanded", "false");
      closeMenus();
    }
  });
})();
