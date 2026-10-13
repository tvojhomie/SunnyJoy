//
//  UniWebViewAndroidDownloadDestination.cs
//  Created by Wang Wei on 2026-08-16.
//
//  This file is a part of UniWebView Project (https://uniwebview.com)
//
//  By purchasing the asset, you are allowed to use this code in as many as projects you want.
//  You can modify it to fit your needs, but you may not redistribute it in source or binary form.
//  You can use it in any commercial or non-commercial projects. But you cannot sell or distribute
//  it as a product or a part of a product.
//
//  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING
//  BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
//  NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM,
//  DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
//  OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

/// <summary>
/// Defines the destination for files downloaded by an Android UniWebView instance.
/// </summary>
public enum UniWebViewAndroidDownloadDestination
{
    /// <summary>
    /// Stores files in the app-specific external Downloads directory. Files are removed when the app is uninstalled.
    /// </summary>
    AppSpecific = 0,

    /// <summary>
    /// Stores files in the user's public Downloads directory.
    /// </summary>
    PublicDownloads = 1,
}
