// Copyright © 2019 The CefSharp Authors. All rights reserved.
//
// Use of this source code is governed by a BSD-style license that can be found in the LICENSE file.

#include "Stdafx.h"
#include "CefURLRequestWrapper.h"

#include "Cef.h"

bool CefURLRequestWrapper::ResponseWasCached()
{
    ThrowIfDisposed();

    return _urlRequest->ResponseWasCached();
}

IResponse^ CefURLRequestWrapper::GetResponse()
{
    ThrowIfDisposed();

    // TODO: Implement
    return nullptr;
}

UrlRequestStatus CefURLRequestWrapper::GetRequestStatus()
{
    ThrowIfDisposed();

    return (UrlRequestStatus)_urlRequest->GetRequestStatus();
}

