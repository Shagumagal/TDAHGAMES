mergeInto(LibraryManager.library, {
  GetDocumentReferrer: function() {
    var ref = "";
    try {
      ref = document.referrer || window.top.location.href || window.location.href;
    } catch(e) {
      ref = document.referrer || window.location.href;
    }
    
    // Unity requires strings to be allocated in its memory space
    var bufferSize = lengthBytesUTF8(ref) + 1;
    var buffer = _malloc(bufferSize);
    stringToUTF8(ref, buffer, bufferSize);
    return buffer;
  }
});
