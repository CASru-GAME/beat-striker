mergeInto(LibraryManager.library, {
  BeatStrikerSyncPlayerRecords: function () {
    if (typeof FS !== 'undefined' && FS.syncfs) {
      Module.beatStrikerRecordsDirty = true;
      if (Module.beatStrikerRecordsSyncing) return;
      Module.beatStrikerRecordsSyncing = true;
      var persist = function () {
        Module.beatStrikerRecordsDirty = false;
      FS.syncfs(false, function (error) {
        if (error) console.error('Player records persistence failed:', error);
          if (Module.beatStrikerRecordsDirty) persist();
          else Module.beatStrikerRecordsSyncing = false;
      });
      };
      persist();
    }
  }
});
