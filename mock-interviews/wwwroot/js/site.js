// Please see documentation at https://docs.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

const populateInterviewerSelectList = (id, interviewType, status) => {
  // console.log('Fetching interviewers...');
  const url = `/InterviewEvents/GetAvailableInterviewers/${id}`;

  $.getJSON(url)
    .done((data) => {
      const behavioralInterviewers = data.behavioralInterviewers;
      const technicalInterviewers = data.technicalInterviewers;

      const $selectElement = $("#InterviewerId");

      $selectElement.empty();

      const interviewers =
        interviewType === "Technical"
          ? technicalInterviewers
          : behavioralInterviewers;
      parseOptions($selectElement, interviewers);

      if (status !== "Ongoing") {
        $("#edit-form-div").show();
      }

      // console.log(behavioralInterviewers, technicalInterviewers);

      localStorage.setItem(
        "behavioralInterviewers",
        JSON.stringify(behavioralInterviewers)
      );
      localStorage.setItem(
        "technicalInterviewers",
        JSON.stringify(technicalInterviewers)
      );
    })
    .fail((jqXHR, textStatus, errorThrown) => {
      console.error("Error fetching data:", textStatus, errorThrown);
    });
};

const parseOptions = ($element, list) => {
  list.forEach((interviewer) => {
    $("<option></option>")
      .val(interviewer.value)
      .text(interviewer.text)
      .appendTo($element);
  });
};

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

const populateInlineForm = () => {
  $(document).on("click", ".capture-data", function (e) {
    e.preventDefault();
    $("html, body").animate(
      {
        scrollTop: $("#edit-form-div").offset().top,
      },
      0
    );

    const row = $(this).closest("tr");

    const id = row.attr("id");
    const studentName = row.data("student-name");
    const interviewerName = row.data("interviewer-name");
    const interviewerId = row.data("interviewer-id");
    const status = row.data("status");
    const interviewType = row.data("type");

    $("#student-name").text(`Assign ${studentName}`);
    $("#Id").val(id);
    $("#Status").val(status);
    $("#Type").val(interviewType);

    const $interviewerSelect = $("#InterviewerId");

    if (interviewerName !== "" && interviewerName !== "Not Assigned") {
      console.log("Student already has interviewer assigned.");
      console.log(
        `InterviewerId: ${interviewerId}, InterviewerName: ${interviewerName}`
      );

      if (
        $interviewerSelect.find(`option[value="${interviewerId}"]`).length === 0
      ) {
        console.log("Appending item...");
        $interviewerSelect.append(
          $("<option>", {
            value: interviewerId,
            text: interviewerName,
          })
        );
      }

      console.log("Setting value...");
      $interviewerSelect.val(interviewerId);
      // $interviewerSelect.prop("disabled", true);
      // $('#Type').prop("disabled", true);
      $("#edit-form-div").show();
    } else {
      populateInterviewerSelectList(id, interviewType, status);
    }
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

const editInterviewAssignments = (technical, behavioral) => {
  $(document).ready(() => {
    $("#interviewType").change(function () {
      const selectedTypeText = $(this).children("option:selected").text();
      const $selectElement = $("#InterviewerId");

      const interviewers =
        selectedTypeText === "Technical" ? technical : behavioral;
      parseOptions($selectElement, interviewers);
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
  populateInlineForm();
});
