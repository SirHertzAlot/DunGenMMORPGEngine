import Map "mo:core/Map";
import Blob "mo:core/Blob";
import Iter "mo:core/Iter";
import List "mo:core/List";
import Text "mo:core/Text";
import Nat8 "mo:core/Nat8";
import Char "mo:core/Char";
import Principal "mo:core/Principal";

module {
  type OldActor = {
    files : Map.Map<Blob, FileMetadataInternal>;
  };

  type NewActor = {
    files : Map.Map<Blob, FileMetadataInternal>;
  };

  public func run(old : OldActor) : NewActor {
    let newFiles = old.files.map<Blob, FileMetadataInternal, FileMetadataInternal>(
      func(_id, oldFile) {
        updateArchiveType(oldFile);
      }
    );
    { files = newFiles };
  };

  func updateArchiveType(file : FileMetadataInternal) : FileMetadataInternal {
    let ext = getFileExtension(file.name);
    func matches(ext : Text, extensions : [Text]) : Bool {
      for (e in extensions.values()) {
        if (e == ext) { return true };
      };
      false;
    };

    switch (matches(ext, archiveExtensions)) {
      case (true) {
        {
          file with
          archiveType = switch (file.archiveType) {
            case (null) { ?#zip };
            case (?t) { ?t };
          };
        };
      };
      case (false) { file };
    };
  };

  func getFileExtension(filename : Text) : Text {
    switch (findLastIndex(filename, '.')) {
      case (null) { "" };
      case (?i) {
        if (i + 1 >= filename.size()) { return "" };
        let chars = filename.toIter().drop(i).toArray();
        let charStr = chars.sliceToArray(1, chars.size());
        Text.fromArray(charStr);
      };
    };
  };

  func findLastIndex(text : Text, target : Char) : ?Nat {
    var index : ?Nat = null;
    let chars = text.toArray();
    let size = chars.size();
    var i = 0;

    while (i < size) {
      if (chars[i] == target) {
        index := ?i;
      };
      i += 1;
    };

    index;
  };

  let archiveExtensions : [Text] = [
    "zip", "tar", "rar", "7z", "gz", "tgz", "bz2", "xz",
    "jar", "war", "ear", "exe", "iso", "dmg",
  ];

  type FileMetadataInternal = {
    id : Blob;
    file : BLOB;
    name : Text;
    size : Nat;
    fileType : FileType;
    uploadTimestamp : Int;
    owner : Principal;
    extractionSource : ?Blob;
    relativePath : Text;
    isDirectory : Bool;
    archiveType : ?ArchiveType;
  };

  type FileType = {
    #GLB;
    #GLTF;
    #OBJ;
    #FBX;
  };

  type ArchiveType = {
    #zip;
    #tar;
  };
};
