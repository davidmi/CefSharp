// Copyright © 2017 The CefSharp Authors. All rights reserved.
//
// Use of this source code is governed by a BSD-style license that can be found in the LICENSE file.

using System;
using System.IO;
using System.Threading;
using CefSharp.OffScreen;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace CefSharp.Test.OffScreen
{
    //NOTE: All Test classes must be part of this collection as it manages the Cef Initialize/Shutdown lifecycle
    [Collection(CefSharpFixtureCollection.Key)]
    public class OffScreenBrowserBasicFacts
    {
        //TODO: Move into own file/namespace
        public class AsyncBoundObject
        {
            public bool MethodCalled { get; set; }
            public string Echo(string arg)
            {
                MethodCalled = true;
                return arg;
            }
        }

        private readonly ITestOutputHelper output;
        private readonly CefSharpFixture fixture;

        public OffScreenBrowserBasicFacts(ITestOutputHelper output, CefSharpFixture fixture)
        {
            this.fixture = fixture;
            this.output = output;
        }

        [Fact]
        public async Task CanLoadGoogle()
        {
            using (var browser = new ChromiumWebBrowser("www.google.com"))
            {
                await browser.LoadPageAsync();

                var mainFrame = browser.GetMainFrame();
                Assert.True(mainFrame.IsValid);
                Assert.True(mainFrame.Url.Contains("www.google"));

                output.WriteLine("Url {0}", mainFrame.Url);
            }
        }

        [Fact]
        public async Task CanLoadGoogleAndEvaluateScript()
        {
            using (var browser = new ChromiumWebBrowser("www.google.com"))
            {
                await browser.LoadPageAsync();

                var mainFrame = browser.GetMainFrame();
                Assert.True(mainFrame.IsValid);
                Assert.True(mainFrame.Url.Contains("www.google"));

                var javascriptResponse = await browser.EvaluateScriptAsync("2 + 2");
                Assert.True(javascriptResponse.Success);
                Assert.Equal(4, (int)javascriptResponse.Result);
                output.WriteLine("Result of 2 + 2: {0}", javascriptResponse.Result);
            }
        }

        [Fact]
        public async Task CrossSiteNavigationJavascriptBinding()
        {
            const string script = @"
                (async function()
                {
                    await CefSharp.BindObjectAsync('bound');
                    bound.echo('test');
                })();";

            var boundObj = new AsyncBoundObject();

            var browser = new ChromiumWebBrowser("https://developer.mozilla.org/en-US/docs/Web/HTML/Element/input/url");
            browser.JavascriptObjectRepository.Register("bound", boundObj, true);

            await browser.LoadPageAsync();
            browser.GetMainFrame().ExecuteJavaScriptAsync(script);

            await Task.Delay(2000);
            Assert.True(boundObj.MethodCalled);

            boundObj.MethodCalled = false;

            browser.Load("https://www.google.com");
            await browser.LoadPageAsync();
            browser.GetMainFrame().ExecuteJavaScriptAsync(script);
            await Task.Delay(2000);
            Assert.True(boundObj.MethodCalled);
        }

        /// <summary>
        /// Use the EvaluateScriptAsync (IWebBrowser, String,Object[]) overload and pass in string params
        /// that require encoding. Test case for https://github.com/cefsharp/CefSharp/issues/2339
        /// </summary>
        /// <returns>A task</returns>
        [Fact]
        public async Task CanEvaluateScriptAsyncWithEncodedStringArguments()
        {
            using (var browser = new ChromiumWebBrowser("http://www.google.com"))
            {
                await browser.LoadPageAsync();

                var mainFrame = browser.GetMainFrame();
                Assert.True(mainFrame.IsValid);

                var javascriptResponse = await browser.EvaluateScriptAsync("var testfunc=function(s) { return s; }");
                Assert.True(javascriptResponse.Success);

                // now call the function we just created
                string[] teststrings = new string[]{"Mary's\tLamb & \r\nOther Things",
                                      "[{test:\"Mary's Lamb & \\nOther Things\", 'other': \"\", 'and': null}]" };
                foreach (var test in teststrings)
                {
                    javascriptResponse = await browser.EvaluateScriptAsync("testfunc", test);
                    Assert.True(javascriptResponse.Success);
                    Assert.Equal(test, (string)javascriptResponse.Result);
                    output.WriteLine("{0} passes {1}", test, javascriptResponse.Result);
                }
            }
        }

        class URLRequestClient : IURLRequestClient
        {
            private Action<IURLRequest, byte[]> CompleteAction;
            private MemoryStream ResponseBody = new MemoryStream();
            private BinaryWriter StreamWriter;

            public URLRequestClient(Action<IURLRequest, byte[]> completeAction)
            {
                CompleteAction = completeAction;
                StreamWriter = new BinaryWriter(ResponseBody);
            }
            public bool GetAuthCredentials(bool isProxy, string host, int port, string realm, string scheme, IAuthCallback callback)
            {
                return true;
            }

            public void OnDownloadData(IURLRequest request, byte[] data)
            {
                StreamWriter.Write(data);
            }

            public void OnDownloadProgress(IURLRequest request, long current, long total)
            {
                return;
            }

            public void OnRequestComplete(IURLRequest request)
            {

                CompleteAction(request, ResponseBody.ToArray());
            }

            public void OnUploadProgress(IURLRequest request, long current, long total)
            {
                return;
            }
        }

        [Fact]
        public async Task CanMakeUrlRequest()
        {
            using (var browser = new ChromiumWebBrowser("https://code.jquery.com/jquery-3.4.1.min.js"))
            {
                await browser.LoadPageAsync();

                var mainFrame = browser.GetMainFrame();
                Assert.True(mainFrame.IsValid);


                IURLRequest urlRequest = null;

                var t = new TaskCompletionSource<string>();
                var wasCached = false;
                var requestClient = new URLRequestClient(
                    (IURLRequest request, byte[] responseBody) =>
                    {
                        wasCached = request.ResponseWasCached();
                        t.TrySetResult(System.Text.Encoding.UTF8.GetString(responseBody));
                    }
                );

                //Make the request on the CEF UI Thread
                await Cef.UIThreadTaskFactory.StartNew(delegate
                {
                    var request = mainFrame.CreateRequest(false);

                    request.Method = "GET";
                    request.Url = "https://code.jquery.com/jquery-3.4.1.min.js";
                    urlRequest = mainFrame.CreateURLRequest(request, requestClient);
                });

                var stringResult = await t.Task;

                Assert.True(!string.IsNullOrEmpty(stringResult));
                Assert.True(wasCached);
            }
        }

    }
}
