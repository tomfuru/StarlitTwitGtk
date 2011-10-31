using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Runtime.Remoting.Messaging;
using System.Reflection;
using System.Threading;
using System.Drawing;
using System.IO;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace StarlitTwitGtk
{
    /// <summary>
    /// 汎用処理です。
    /// </summary>
    public static class Utilization
    {
        public const char CHR_LOCKED = '◆';
        public const char CHR_FAVORITED = '★';
        public const string STR_DATETIMEFORMAT = "yyyy/MM/dd HH:mm:ss";
        public const string URL_REGEX_PATTERN = @"h?(ttp|ttps)://[-_!~*'()0-9a-zA-Z;@&=+$,%]+\.[a-zA-Z]+[-_.!~*'()0-9a-zA-Z;?:@&=+$,%#/]*";

        public const string SAVEFILE_NAME = @"Settings.dat";

        //-------------------------------------------------------------------------------
        #region +[static]UrlEncode
        //-------------------------------------------------------------------------------
        //
        /// <remarks>参考/利用：http://d.hatena.ne.jp/nojima718/20100129/1264792636 </remarks>
        public static string UrlEncode(string value)
        {
            string unreserved = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_.~";
            StringBuilder result = new StringBuilder();
            byte[] data = Encoding.UTF8.GetBytes(value);
            foreach (byte b in data) {
                if (b < 0x80 && unreserved.IndexOf((char)b) != -1)
                    result.Append((char)b);
                else
                    result.Append('%' + String.Format("{0:X2}", (int)b));
            }
            return result.ToString();
        }
        //-------------------------------------------------------------------------------
        #endregion (UrlEncode)

        //-------------------------------------------------------------------------------
        #region +[static]CountTextLength 投稿時の文字列長を計算します。
        //-------------------------------------------------------------------------------
        /// <summary>
        /// 投稿時の文字列長を計算します。長いURLはhttp://t.co/*******に変換されたとして計算します。
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static int CountTextLength(string str)
        {
            int len = str.Length;
            foreach (Match m in Regex.Matches(str, URL_REGEX_PATTERN)) {
                len -= Math.Max(m.Length - 19, 0);
            }
            return len;
        }
        #endregion (CountTextLength)

        //-------------------------------------------------------------------------------
        #region +[static]GetImageFromURL 画像取得
        //-------------------------------------------------------------------------------
        /// <summary>
        /// 指定URLから画像を取得します。
        /// </summary>
        /// <param name="url">画像URL</param>
        /// <returns></returns>
        public static Image GetImageFromURL(string url)
        {
            WebClient wc = new WebClient();

            Bitmap bmp = null;

            try {
                using (Stream stream = wc.OpenRead(url))
                using (Image img = Image.FromStream(stream)) {
                    bmp = new Bitmap(img);
                }
            }
            catch (WebException) { return null; }
            catch (ArgumentException) { return null; }

            return bmp;
        }
        #endregion (GetImageFromURL)

        //-------------------------------------------------------------------------------
        #region +[static]SubTwitterAPIExceptionStr TwitterAPI例外の文字列を返します。
        //-------------------------------------------------------------------------------
        //
        public static string SubTwitterAPIExceptionStr(TwitterAPIException ex)
        {
            switch (ex.ErrorStatusCode) {
                case 0:
                    // Connection Failure
                    return "ネットワークに接続されていない可能性があります。";
                case 1:
                    // Disconnected
                    return "接続が切断されました。";
                case 400:
                    // Bad Request
                    return "APIの実行制限の可能性があります。";
                case 401:
                    // Not Authorized
                    return "認証に失敗しました。";
                case 403:
                    // Forbidden
                    return "使用できないAPIです。";
                case 404:
                    // Not Found
                    return "見つからないAPIです。";
                case 408:
                    // Request Timeout
                    return "要求がタイムアウトしました";
                case 500:
                    // Internal Server Error
                    return "Twitterのサーバーに問題があります。";
                case 502:
                    // Bad Gateway
                    return "Twitterのサーバーが停止しています。";
                case 503:
                    // Service Unavailable
                    return "Twitterが高負荷によりダウンしています。";
                case 1000:
                    // Faliure XmlLoad
                    return "予期しないデータを取得しました。";
                case 1001:
                    // Unexpected Xml
                    return "予期しないXmlデータを取得しました。";
                default:
                    //Log.DebugLog(ex);
                    return "不明なエラーです。";
            }
        }
        #endregion (SubTwitterAPIExceptionStr)

        //-------------------------------------------------------------------------------
        #region +[static]GetHostName URLからホスト名を取得します。
        //-------------------------------------------------------------------------------
        /// <summary>
        /// URLからホスト名を取得します。
        /// </summary>
        /// <param name="url"></param>
        public static string GetHostName(string url)
        {
            const string TTP = @"ttp://";
            const string HTTP = @"http://";
            //const string HTTPS = @"https://";

            int start;
            if (url.StartsWith(HTTP)) { start = HTTP.Length; }
            else if (url.StartsWith(TTP)) { start = TTP.Length; }
            //else if (url.StartsWith(HTTPS)) { start = HTTPS.Length; }
            else { return null; }

            int end = url.IndexOf('/', start);
            if (end == -1) { end = url.Length; }

            return url.Substring(start, end - start);
        }
        #endregion (GetHostName)

        //-------------------------------------------------------------------------------
        #region +[static]InvokeTransaction 処理を別スレッドで行います。
        //-------------------------------------------------------------------------------
        /// <summary>
        /// 処理を別スレッドで行います。
        /// </summary>
        /// <param name="act">別スレッドで行いたい処理</param>
        /// <param name="endAct">[option]処理終了時に1回だけ行う処理</param>
        public static IAsyncResult InvokeTransaction(Action act, Action endAct = null)
        {
            return act.BeginInvoke((ar) =>
            {
                Utilization.InvokeCallback(ar);
                if (endAct != null) { endAct(); }
            }
            , null);
        }
        #endregion (InvokeTransaction)

        //-------------------------------------------------------------------------------
        #region +[static]InvokeCallback Invoke完了時に呼び出すメソッド
        //-------------------------------------------------------------------------------
        /// <summary>
        /// EndInvokeを動的に呼び出します。
        /// </summary>
        /// <param name="ar"></param>
        public static void InvokeCallback(IAsyncResult ar)
        {
            AsyncResult asyncResult = (AsyncResult)ar;

            dynamic delg = asyncResult.AsyncDelegate;
            try {
                delg.EndInvoke(ar);
            }
            catch (TargetInvocationException ex) {
                throw ex.InnerException;
            }
            catch (Exception ex) {
                throw ex;
            }
        }
        #endregion (Callback)

        //-------------------------------------------------------------------------------
        #region +[static]ExtractURL URL部分を抜き出します
        //-------------------------------------------------------------------------------
        /// <summary>
        /// テキストからURL部分を抜き出します。
        /// </summary>
        /// <param name="text">抜き出すテキスト</param>
        /// <returns>URLの配列。</returns>
        public static IEnumerable<string> ExtractURL(string text)
        {
            #region comment out
            //const string HTTP = @"http://";
            //const string ENDCHARS = " 　";

            //int index = 0;

            //while (true) {
            //    int start = (index < text.Length) ? text.IndexOf(HTTP, index) : -1;
            //    if (start == -1) { break; }
            //    int end = text.IndexOfAny(ENDCHARS.ToCharArray(), start);
            //    if (end == -1) { end = text.Length; }

            //    string url = text.Substring(start, end - start);
            //    yield return url;
            //    index = end + 1;
            //}
            #endregion

            MatchCollection mc = Regex.Matches(text, URL_REGEX_PATTERN);
            foreach (Match match in mc) {
                yield return match.Value;
            }
        }
        #endregion (ExtractURL)

        //-------------------------------------------------------------------------------
        #region +[static]Swap 値を交換します。
        //-------------------------------------------------------------------------------
        /// <summary>
        /// 値を交換します。
        /// </summary>
        /// <typeparam name="T">型</typeparam>
        /// <param name="val1">値1</param>
        /// <param name="val2">値2</param>
        public static void Swap<T>(ref T val1, ref T val2)
        {
            T tmp = val1;
            val1 = val2;
            val2 = tmp;
        }
        #endregion (Swap)
        //-------------------------------------------------------------------------------
        #region +[static]EmptyIEnumerable 空の要素を列挙します。
        //-------------------------------------------------------------------------------
        /// <summary>
        /// 任意の型の空の要素をIEnumerableジェネリックとして列挙します。
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static IEnumerable<T> EmptyIEnumerable<T>()
        {
            yield break;
        }
        #endregion (EmptyIEnumerable)
    }
}

public static class Extension
{
    public static IEnumerable<T> AsEnumerable<T>(this T val)
    {
        yield return val;
    }
}