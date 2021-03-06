// This file is the part of the Indexer++ project.
// Copyright (C) 2016 Anna Krykora <krykoraanna@gmail.com>. All rights reserved.
// Use of this source code is governed by a MIT-style license that can be found in the LICENSE file.

#include "TestsHelperFunctions.h"

using namespace std;

SearchQuery* CopyQuery(const SearchQuery& other) {

    auto copied =
        new SearchQuery(other.Text, other.SearchDirPath, other.MatchCase, other.SizeFrom, other.SizeTo,
                        other.ExcludeHiddenAndSystem, other.ExcludeFolders, other.ExcludeFiles, other.CTimeFrom,
                        other.CTimeTo, other.ATimeFrom, other.ATimeTo, other.MTimeFrom, other.MTimeTo);

    return copied;
}
