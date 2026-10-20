// zip.js - 纯 JS ZIP 打包（STORE 模式，含 CRC32），浏览器/Node 双端
(function (root, factory) {
  if (typeof module === 'object' && module.exports) { module.exports = factory(); }
  else { root.MCZip = factory(); }
})(typeof self !== 'undefined' ? self : this, function () {

  var CRC_TABLE = (function () {
    var t = new Uint32Array(256);
    for (var n = 0; n < 256; n++) {
      var c = n;
      for (var k = 0; k < 8; k++) { c = (c & 1) ? (0xEDB88320 ^ (c >>> 1)) : (c >>> 1); }
      t[n] = c >>> 0;
    }
    return t;
  })();

  function crc32(bytes) {
    var c = 0xFFFFFFFF;
    for (var i = 0; i < bytes.length; i++) { c = CRC_TABLE[(c ^ bytes[i]) & 0xFF] ^ (c >>> 8); }
    return (c ^ 0xFFFFFFFF) >>> 0;
  }

  function utf8(str) {
    var out = [], i, c;
    for (i = 0; i < str.length; i++) {
      c = str.charCodeAt(i);
      if (c < 0x80) out.push(c);
      else if (c < 0x800) { out.push(0xC0 | (c >> 6), 0x80 | (c & 63)); }
      else { out.push(0xE0 | (c >> 12), 0x80 | ((c >> 6) & 63), 0x80 | (c & 63)); }
    }
    return new Uint8Array(out);
  }

  /**
   * files: [{path: 'a/b.cs', content: 'string'}] -> Uint8Array (zip)
   */
  function makeZip(files) {
    var chunks = [], central = [], offset = 0, i;

    function w16(v) { chunks.push(new Uint8Array([v & 0xFF, (v >> 8) & 0xFF])); }
    function w32(v) { chunks.push(new Uint8Array([v & 0xFF, (v >> 8) & 0xFF, (v >> 16) & 0xFF, (v >>> 24) & 0xFF])); }
    function push(a) { chunks.push(a); }

    for (i = 0; i < files.length; i++) {
      var nameB = utf8(files[i].path);
      var dataB = utf8(files[i].content);
      var crc = crc32(dataB);

      // local file header
      w32(0x04034b50); w16(20); w16(0x0800); w16(0); w16(0); w16(0x21);
      w32(crc); w32(dataB.length); w32(dataB.length);
      w16(nameB.length); w16(0);
      push(nameB); push(dataB);

      // central directory record
      var cd = [];
      function cw16(v) { cd.push(new Uint8Array([v & 0xFF, (v >> 8) & 0xFF])); }
      function cw32(v) { cd.push(new Uint8Array([v & 0xFF, (v >> 8) & 0xFF, (v >> 16) & 0xFF, (v >>> 24) & 0xFF])); }
      cw32(0x02014b50); cw16(20); cw16(20); cw16(0x0800); cw16(0); cw16(0); cw16(0x21);
      cw32(crc); cw32(dataB.length); cw32(dataB.length);
      cw16(nameB.length); cw16(0); cw16(0); cw16(0); cw16(0); cw32(0); cw32(offset);
      central.push(concatU8(cd)); central.push(nameB);

      offset += 30 + nameB.length + dataB.length;
    }

    var centralSize = 0, j;
    for (j = 0; j < central.length; j++) centralSize += central[j].length;

    push(concatU8(central));
    // EOCD
    w32(0x06054b50); w16(0); w16(0); w16(files.length); w16(files.length);
    w32(centralSize); w32(offset); w16(0);

    return concatU8(chunks);
  }

  function concatU8(arr) {
    var len = 0, i, out, p = 0;
    for (i = 0; i < arr.length; i++) len += arr[i].length;
    out = new Uint8Array(len);
    for (i = 0; i < arr.length; i++) { out.set(arr[i], p); p += arr[i].length; }
    return out;
  }

  return { makeZip: makeZip, crc32: crc32 };
});
