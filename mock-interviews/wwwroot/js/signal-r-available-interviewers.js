$(document).ready(() => {
    const connection = new signalR.HubConnectionBuilder().withUrl("/interviewershub").build();

    connection.start().catch(err => console.error(err.toString()));

    connection.on("ReceiveAvailableInterviewersUpdate", (interviewers) => {
        console.log('Received available interviewers:', interviewers);
        const $selectBox = $('#InterviewerId');

        if ($selectBox.children('option').length !== 0) {
            const id = $('#Id').val();
            const type = $('#Type').val();
            const status = $('#Status').val();
            populateInterviewerSelectList(id, type, status);
        }

        const $availableInterviewersView = $('#availableInterviewers');
        $availableInterviewersView.empty();

        interviewers.forEach(interviewer => {
            const $newListItem = createInterviewerCard(interviewer);
            $availableInterviewersView.append($newListItem);
        });
    });

    // Helper function to create a list item for an interviewer
    const createInterviewerCard = (interviewer) => {
        const $card = $('<div></div>')
            .addClass('interviewer-card')
            .attr('id', interviewer.id)

        const $cardinner = $('<div></div>')
            .addClass('card-inner')

        const $cardfront = $('<div></div>')
            .addClass('card-front');

        const $cardback = $('<div></div>')
            .addClass('card-back');

        const $name = $('<div></div>').text(interviewer.name).addClass('interviewer-name');
        const $type = $('<div></div>').text(interviewer.interviewType).addClass('interview-type');
        const $room = $('<div></div>').text(interviewer.room).addClass('room-location');

        $cardfront.append($name, $type);
        $cardback.append($room);

        $cardinner.append($cardfront, $cardback);
    
        $card.append($cardinner);
    
        return $card;
    };    
});
