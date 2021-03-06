// This file is the part of the Indexer++ project.
// Copyright (C) 2016 Anna Krykora <krykoraanna@gmail.com>. All rights reserved.
// Use of this source code is governed by a MIT-style license that can be found in the LICENSE file.

#pragma once

#include <list>
#include <string>
#include <vector>

#include "Macros.h"

#include "IndexChangeObserver.h"
#include "IndexManager.h"

class Index;
class FileInfo;
class StatusObserver;


// Container class for index managers. A layer between index managers (and corresponding indices) and search engine.

class IndexManagersContainer : public IndexChangeObserver {

   public:
    NO_COPY(IndexManagersContainer)


    static IndexManagersContainer& Instance();


    void AddDrive(char drive_letter);


    void RemoveDrive(char drive_letter);


    std::vector<const IndexManager*> GetAllIndexManagers() const;


    const IndexManager* GetIndexManager(char drive_letter) const;


    // This is the helper function which is needed in the Model and in the SearchEngine.
    // If the number of FileInfo or file system helper functions will increase - extract them in the separate class.

    const FileInfo* GetFileInfoByPath(const std::u16string& path) const;


#ifdef SINGLE_THREAD
    void CheckUpdates();
#endif


    virtual void OnIndexChanged(pNotifyIndexChangedEventArgs p_args) override;


    virtual void OnVolumeStatusChanged(char drive_letter) override;


    void RegisterIndexChangeObserver(IndexChangeObserver* observer);


    void UnregisterIndexChangeObserver(IndexChangeObserver* observer);


    void RegisterStatusChangeObserver(StatusObserver* observer);


    void UnregisterStatusChangeObserver(StatusObserver* observer);


    uint GetIndexRootID(char index_drive_letter) const;


   private:
    IndexManagersContainer();


    // Creates a status string, which describes which volumes loaded and which are still in read MFT state.

    std::string GetStatus() const;


    std::vector<uIndexManager> index_managers_;

    std::list<IndexChangeObserver*> index_change_observers_;

    std::list<StatusObserver*> status_observers_;


    Log* logger_;
};