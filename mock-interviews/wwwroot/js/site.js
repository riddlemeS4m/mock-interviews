// Please see documentation at https://docs.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

const applyTimers = function () {
  $('[id^="timer-"]').each(function () {
    const $timer = $(this);
    let startTimeString = $timer.text().trim();

    const startTime = new Date(startTimeString);

    if (isNaN(startTime)) {
      return;
    }

    const updateTimer = function () {
      const now = Date.now();
      const elapsedMs = now - startTime.getTime();

      const hours = Math.floor(elapsedMs / (3600 * 1000));
      const minutes = Math.floor((elapsedMs % (3600 * 1000)) / (60 * 1000));
      const seconds = Math.floor((elapsedMs % (60 * 1000)) / 1000);

      const formattedTime =
        `${hours.toString().padStart(2, "0")}:` +
        `${minutes.toString().padStart(2, "0")}:` +
        `${seconds.toString().padStart(2, "0")}`;
      $timer.text(formattedTime);

      if (minutes >= 30 || hours > 0) {
        $timer.css({
          color: "red",
          fontWeight: "bold",
        });
      } else {
        $timer.css({
          color: "",
          fontWeight: "",
        });
      }
    };

    updateTimer();
    setInterval(updateTimer, 1000);
  });
};

const interviewerSelfCheckIn = (status) => {
  $(document).ready(() => {
    if (status) {
      $("#exampleModalCenter").modal("show");
    }

    $("#hideModalButton").click(() => {
      $("#exampleModalCenter").modal("hide");
    });
  });
};

const displayResources = () => {
  $("#manual-button").on("click", () => {
    $(this).addClass("disabled");
  });

  $("#parking-button").on("click", () => {
    $(this).addClass("disabled");
  });
};

const selectAll = () => {
  $(document).ready(() => {
    $(".selectAllButton").on("click", function () {
      const target = $(this).data("target");
      const $checkboxes = $(`input[name="${target}"]`);
      const allChecked = $checkboxes
        .toArray()
        .every((checkbox) => checkbox.checked);

      $checkboxes.prop("checked", !allChecked);
    });
  });
};

const toggleLunchQuestion = () => {
  $(document).ready(() => {
    const $checkbox1 = $("#InPerson[value='true']");
    const $checkbox2 = $("#InPerson[value='false']");
    const $checkbox2Label = $("#annoyingLabel");
    const $divToToggle = $("#lunch-question");

    $checkbox1.on("click", () => {
      $divToToggle.show();
    });

    $checkbox2.on("click", () => {
      $divToToggle.hide();
    });

    $checkbox2Label.on("click", () => {
      $checkbox2.prop("checked", !$checkbox2.prop("checked"));
      $divToToggle.hide();
    });
  });
};

$(document).ready(() => {
  applyTimers();
});
