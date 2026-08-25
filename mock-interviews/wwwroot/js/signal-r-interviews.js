$(document).ready(() => {
  const connection = new signalR.HubConnectionBuilder()
    .withUrl("/interviewHub")
    .build();

  connection.start().catch((err) => console.error(err.toString()));

  connection.on(
    "ReceiveInterviewEventUpdate",
    (interview, studentName, studentClass, interviewerId, interviewerName, time, date) => {
      console.log(`Received interview update for interview ${interview.id}...`);
      console.log(`Interview status is ${interview.status}`);
      const $row = $(`#${interview.id}`);

      if (studentName === "delete") {
        $row.remove();
        $("#edit-form-div").hide();
        return;
      }

      if (interview.status === "Ongoing") {
        $("#edit-form-div").hide();
      }

      if ($row.length) {
        const $newRow = createInterviewRow(
          interview,
          studentName,
          studentClass,
          interviewerId,
          interviewerName,
          time,
          date
        );

        // Append the new row to the appropriate table based on the status
        if (interview.status === "Ongoing") {
          $row.remove();
          $("#ongoing .dataTables_empty").remove();
          $("#ongoing").append($newRow);
          clearEmptyRow("#ongoing", 9);
        } else if (interview.status === "Checked In") {
          $row.remove();
          $("#checkin .dataTables_empty").remove();
          $("#checkin").append($newRow);
          clearEmptyRow("#checkin", 7);
        } else {
          $row.remove();
          $("#upcoming").append($newRow);
          clearEmptyRow("#upcoming", 7);
        }

        $(`${interview.id}`).on("mouseenter", function () {
          this.querySelectorAll("td").forEach((td) => {
            td.style.setProperty("background-color", "#f5f5f5", "important");
          });
        });

        $(`${interview.id}`).on("mouseleave", function () {
          this.querySelectorAll("td").forEach((td) => {
            td.style.removeProperty("background-color");
          });
        });

        $(".date-column").each(function () {
          let date = new Date($(this).text().trim());
          if (!isNaN(date.getTime())) {
            $(this).text(date.toLocaleDateString());
          }
        });

        $(".time-column").each(function () {
          let date = new Date($(this).text().trim());
          if (!isNaN(date.getTime())) {
            $(this).text(
              date.toLocaleTimeString([], {
                hour: "numeric",
                minute: "2-digit",
              })
            );
          }
        });
        
        console.log(`Received interview start time: ${interview.startedAt}...`)
        console.log(`Received interview check in time: ${interview.checkedInAt}...`);
        applyTimers();
        populateInlineForm();
      }
    }
  );

  // Helper function to create a row dynamically
  const createInterviewRow = (
    interview,
    studentName,
    studentClass,
    interviewerId,
    interviewerName,
    time,
    date
  ) => {
    console.log(`Will create a row for a '${interview.status}' interview...`);
    console.log(`Student Class: ${studentClass}, Student Name: ${studentName}`);
    const $row = $("<tr></tr>")
      .attr("id", interview.id)
      .data("interviewer-name", interviewerName)
      .data("interviewer-id",interviewerId)
      .data("student-name", studentName)
      .data("status", interview.status)
      .data("type", interview.type);

    $row.append(
      createCell(interview.timeslotId, {
        class: "sorting_1",
        style: "color:white;",
      })
    );
    $row.append(createCell(studentName));

    if (interview.status === "Ongoing") {
      console.log("Condition met: Ongoing");
      $row.append(createCell(interview.status));
      $row.append(createCell(interviewerName));
      $row.append(createCell(interview.location.room));
      $row.append(createCell(time, { class: "time-column" }));
      $row.append(
        createCell(interview.startedAt, { id: `timer-${interview.id}` })
      );
      $row.append(createCell(interview.type));
      $row.append(
        createActionsCell([
          {
            text: "Override",
            href: `/InterviewEvents/Override/${interview.id}`,
            style: "color:#9E1B32",
          },
          {
            text: "Complete",
            href: `/InterviewEvents/StudentComplete/${interview.id}`,
            style: "color:#9E1B32",
          },
        ])
      );
    } else if (interview.status === "Checked In") {
      console.log("Condition met: Checked In");
      $row.append(createCell(studentClass));
      $row.append(createCell(interview.status));
      $row.append(createCell(time, { class: "time-column" }));
      $row.append(
        createCell(interview.checkedInAt, { id: `timer-${interview.id}` })
      );
      $row.append(createCell(interview.type));
      $row.append(
        createActionsCell([
          { text: "Assign", class: "capture-data" },
          {
            text: "Override",
            href: `/InterviewEvents/Override/${interview.id}`,
            style: "color:#9E1B32",
          },
          {
            text: "Complete",
            href: `/InterviewEvents/StudentComplete/${interview.id}`,
            style: "color:#9E1B32",
          },
        ])
      );
    } else {
      console.log(`Interview Status turned out to be: ${interview.status}`);
      $row.append(createCell(studentClass));
      $row.append(createCell(interview.status));
      $row.append(createCell(time, { class: "time-column" }));
      $row.append(createCell(date, { class: "date-column" }));
      $row.append(createCell(interview.type));
      $row.append(
        createActionsCell([
          {
            text: "Check In",
            href: `/InterviewEvents/StudentCheckIn/${interview.id}`,
            style: "color:#9E1B32",
          },
          {
            text: "Override",
            href: `/InterviewEvents/Override/${interview.id}`,
            style: "color:#9E1B32",
          },
          {
            text: "No Show",
            href: `/InterviewEvents/StudentNoShow/${interview.id}`,
            style: "color:#9E1B32",
          },
        ])
      );
    }

    console.log($row);

    return $row;
  };

  // Helper function to create a table cell
  const createCell = (content, attributes = {}) => {
    console.log(`Creating cell for ${content}...`);
    const $cell = $("<td></td>").text(content);

    Object.entries(attributes).forEach(([key, value]) => {
      $cell.attr(key, value);
    });

    return $cell;
  };

  // Helper function to create a cell with action links
  const createActionsCell = (actions) => {
    const $cell = $("<td></td>").css("width", `${actions.length * 50}px`);

    actions.forEach((action) => {
      const $link = $("<a></a>").text(action.text);

      if (action.href) {
        $link.attr("href", action.href);
      }
      if (action.style) {
        $link.attr("style", action.style);
      }
      if (action.class) {
        $link.addClass(action.class);
      }

      $cell.append($link);

      if (actions.indexOf(action) < actions.length - 1) {
        $cell.append(" | ");
      }
    });

    return $cell;
  };

  // Helper function to remove empty rows
  const clearEmptyRow = (selector, colspan) => {
    const $emptyRow = $(`${selector} td[colspan="${colspan}"]`).parent();
    if ($emptyRow.length) {
      $emptyRow.remove();
    }
  };
});
