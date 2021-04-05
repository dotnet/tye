/** Scroll to the bottom of the input element */
function logviewScrollToBottom(elementId) { 
    var elem = document.getElementById(elementId);
    
    elem.scrollTop = elem.scrollHeight;
}