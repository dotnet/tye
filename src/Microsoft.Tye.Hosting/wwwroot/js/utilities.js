/** Scroll to the bottom of the input element */
function scrollToBottom(elementId) { 
    var elem = document.getElementById(elementId);
    
    elem.scrollTop = elem.scrollHeight;
}