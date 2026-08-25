function populateSelectBox(selectBox, items) {
    selectBox.empty();

    $.each(items, (item) => {
        selectBox.append($('<option>', {
            value: item.value,
            text: item.text
        }));
    });
}

// Function to build the success message using styled spans
function buildSuccessMessage(data) {
    const container = document.createElement('div');

    const span1 = document.createElement('span');
    span1.textContent = 'Successfully submitted! ';
    container.appendChild(span1);

    const studentName = document.createElement('span');
    studentName.textContent = data.studentName;
    studentName.style.fontWeight = 'bold';
    container.appendChild(studentName);

    const span2 = document.createElement('span');
    span2.textContent = ' has a ';
    container.appendChild(span2);

    const interviewType = document.createElement('span');
    interviewType.textContent = data.interviewType;
    interviewType.style.fontWeight = 'bold';
    container.appendChild(interviewType);

    const span3 = document.createElement('span');
    span3.textContent = ' interview with ';
    container.appendChild(span3);

    const interviewerName = document.createElement('span');
    interviewerName.textContent = data.interviewerName;
    interviewerName.style.fontWeight = 'bold';
    interviewerName.style.textDecoration = 'underline';
    container.appendChild(interviewerName);

    const span4 = document.createElement('span');
    span4.textContent = ' in ';
    container.appendChild(span4);

    const location = document.createElement('span');
    location.textContent = data.location;
    location.style.fontWeight = 'bold';
    location.style.textDecoration = 'underline';
    container.appendChild(location);

    const span5 = document.createElement('span');
    span5.textContent = '.';
    container.appendChild(span5);

    return container;
}


$(document).ready(() => {
    // Switching inline dropdown menu between technical and behavioral interviewers
    $('#Type').change(function () {
        const selectedTypeText = $(this).children("option:selected").text();
        const interviewerIdSelect = $('#InterviewerId');

        const interviewerLists = {
            Technical: JSON.parse(localStorage.getItem('technicalInterviewers')),
            Behavioral: JSON.parse(localStorage.getItem('behavioralInterviewers')),
        };

        if (interviewerLists[selectedTypeText]) {
            populateSelectBox(interviewerIdSelect, interviewerLists[selectedTypeText]);
        } else {
            console.error('Invalid type selected or interviewer lists missing.');
        }
    });

    // Event listener for hiding the form
    $('#hide-button').on('click', () => {
        $('#editForm')[0].reset();
        $('#edit-form-div').hide();
    });

    // Event listener for "Done" button in the modal
    $('#hideModalButton').on('click', () => {
        $('#exampleModalCenter').modal('hide');
    });

    // Event listener for form submission
    $('#editForm').on('submit', function (event) {
        event.preventDefault();

        const formData = new FormData(this);

        $.ajax({
            url: '/InterviewEvents/EditInline',
            method: 'POST',
            data: formData,
            processData: false, // Prevent jQuery from automatically processing the data
            contentType: false, // Prevent jQuery from setting the content type header
            success: function (data) {
                // console.log(data);

                $('#edit-form-div').hide();

                const successMessage = buildSuccessMessage(data);

                $('#successText').empty().append(successMessage);

                $('#exampleModalCenter').modal('show');
            },
            error: function (xhr, status, error) {
                console.error('Form submission failed:', error);
            }
        });
    });
});