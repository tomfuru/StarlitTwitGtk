using System;
using System.Linq;
using System.Collections.Generic;
using System.Data.Linq;
using System.Threading;
using Gtk;
using StarlitTwitGtk;

public partial class MainWindow : Gtk.Window
{
    private SettingsData _settingsData = null;
    private Twitter _twitter = new Twitter();
    private NodeStore _store = new NodeStore(typeof(NodeTweetData));
    private bool _isAuthenticated = false;
    private SortedList<long, NodeTweetData> _tweetList;
    private readonly Longcomp LONGCOMP = new Longcomp();
    //-------------------------------------------------------------------------------
    #region (Class)Longcomp longの比較クラス
    //-------------------------------------------------------------------------------
    //
    private class Longcomp : IComparer<long>
    {
        public int Compare(long x, long y)
        {
            return -Math.Sign(x - y);
        }
    }
    #endregion ((Class)longcomp)

    //----------------------------------------------------------------------------------------
    #region MainWindow Constructor
    //----------------------------------------------------------------------------------------
    public MainWindow() : base(Gtk.WindowType.Toplevel)
    {
        this.Build();

        this.nodeview1.DoubleBuffered = true;
        this.lblMe.Text = "(未認証)";
        this.lblstatus.Text = "";
        lblUserStream.Text = "";

        _tweetList = new SortedList<long, NodeTweetData>(LONGCOMP);

        _settingsData = SettingsData.Restore(Utilization.SAVEFILE_NAME);
        if (_settingsData == null) {
            _settingsData = new SettingsData();
        }

        if (_settingsData.AuthInfo.Length > 0) {
            SetAuthenticateData(_settingsData.AuthInfo[0]);
        }

        int columnNum = 0;
        // Name
        this.nodeview1.AppendColumn(MakeTreeViewColumn("ScreenName", columnNum++, 150));
        this.nodeview1.AppendColumn(MakeTreeViewColumn("UserName", columnNum++, 100));
        this.nodeview1.AppendColumn(MakeTreeViewColumn("Datetime", columnNum++, 150));
        this.nodeview1.AppendColumn(MakeTreeViewColumn("Client", columnNum++, 100));
        this.nodeview1.AppendColumn(MakeTreeViewColumn("Text", columnNum++));
        this.nodeview1.NodeStore = _store;
        
        //_store.AddNode(new NodeTweetData("aaa", "bbb"));

        Thread t = new Thread(BackGroundThread);
        t.IsBackground = true;
        t.Start();
    }
    //----------------------------------------------------------------------------------------
    #endregion

    //----------------------------------------------------------------------------------------
    #region OnDeleteEvent
    //----------------------------------------------------------------------------------------
    protected void OnDeleteEvent(object sender, DeleteEventArgs a)
    {
        _settingsData.Save(Utilization.SAVEFILE_NAME);
        Application.Quit();
        a.RetVal = true;
    }
    //----------------------------------------------------------------------------------------
    #endregion

    //----------------------------------------------------------------------------------------
    #region OnBtnUpdateClicked 投稿
    //----------------------------------------------------------------------------------------
    protected void OnBtnUpdateClicked(object sender, System.EventArgs e)
    {
        try {
            entTweet.IsEditable = false;
            _twitter.statuses_update(entTweet.Text);
            entTweet.Text = "";
            _homeRenew_IsForce = true;
        } catch (TwitterAPIException ex) {
            this.lblstatus.Text = Utilization.SubTwitterAPIExceptionStr(ex);
        } finally {
            entTweet.IsEditable = true;
        }
    }
    #endregion

    //----------------------------------------------------------------------------------------
    #region 認証_OnActionActivated
    //----------------------------------------------------------------------------------------
    protected void OnActionActivated(object sender, System.EventArgs e)
    {

        string req_token, req_token_secret;
        req_token = _twitter.oauth_request_token(out req_token_secret);

        string authURL = _twitter.oauth_authorize_URL(req_token);

        System.Diagnostics.Process.Start(authURL);


        string pin = null;
        // Dialog
        using (DialogAuth da = new DialogAuth()) {
            da.Response += (object o, ResponseArgs args) => {
                if (args.ResponseId == ResponseType.Ok) {
                    pin = da.PIN;
                }
            };
            da.Parent = this;
            da.Modal = true;
            da.Run();
            da.Destroy();
        }

        if (pin != null) {
            UserAuthInfo authInfo = _twitter.oauth_access_token(pin, req_token, req_token_secret);
            _settingsData.AuthInfo = new UserAuthInfo[] { authInfo };
            SetAuthenticateData(authInfo);
            _settingsData.Save(Utilization.SAVEFILE_NAME);
        }
    }
    //----------------------------------------------------------------------------------------s
    #endregion

    //----------------------------------------------------------------------------------------
    #region OnActionRenewActivated
    //----------------------------------------------------------------------------------------
    protected void OnActionRenewActivated(object sender, System.EventArgs e)
    {
        _homeRenew_IsForce = true;
    }
    //----------------------------------------------------------------------------------------
    #endregion (OnActionRenewActivated)

    //----------------------------------------------------------------------------------------
    #region OnEntTweetKeyPressEvent
    //----------------------------------------------------------------------------------------
    [GLib.ConnectBeforeAttribute]
    protected void OnEntTweetKeyPressEvent(object o, Gtk.KeyPressEventArgs args)
    {
        if (args.Event.Key == Gdk.Key.Return && entTweet.Text.Length > 0) {
            OnBtnUpdateClicked(o, args);
        }
    }
    //----------------------------------------------------------------------------------------
    #endregion (OnEntTweetKeyPressEvent)

    //----------------------------------------------------------------------------------------
    #region -MakeTreeViewColumn
    //----------------------------------------------------------------------------------------
    private TreeViewColumn MakeTreeViewColumn(string title, int columnNo, int maxWidth = 0)
    {
        var t = new TreeViewColumn(title, new CellRendererText() { Alignment = Pango.Alignment.Left, Scale = 0.8 }, "text", columnNo);
        t.Resizable = true;
        t.Expand = false;

        if (maxWidth > 0) {
            t.MaxWidth = maxWidth;
        }

        return t;
    }
    //----------------------------------------------------------------------------------------
    #endregion (MakeTreeViewColumn)

    //----------------------------------------------------------------------------------------
    #region -SetAuthenticateData 認証データ設定
    //----------------------------------------------------------------------------------------
    private void SetAuthenticateData(UserAuthInfo authinfo)
    {
        _twitter.AccessToken = authinfo.AccessToken;
        _twitter.AccessTokenSecret = authinfo.AccessTokenSecret;
        _twitter.ID = authinfo.ID;
        _twitter.ScreenName = authinfo.ScreenName;

        _isAuthenticated = true;
        lblMe.Text = "(未取得)";
    }
    #endregion

    //----------------------------------------------------------------------------------------
    #region -GetProfile
    //----------------------------------------------------------------------------------------
    private void GetProfile()
    {
        try {
            this.lblstatus.Text = "Profile取得中...";
            UserProfile prof = _twitter.users_show(screen_name: _twitter.ScreenName);
            lblMe.Text = string.Format("{0}/{1} Friend:{2} Follower:{3} Statuses:{4}", prof.ScreenName, prof.UserName, prof.FriendNum, prof.FollowerNum, prof.StatusNum);
            this.lblstatus.Text = "Profile取得完了";
        } catch (TwitterAPIException ex) {
            this.lblstatus.Text = Utilization.SubTwitterAPIExceptionStr(ex);
        }
    }
    //----------------------------------------------------------------------------------------
    #endregion

    //----------------------------------------------------------------------------------------
    #region -GetHome
    //----------------------------------------------------------------------------------------
    private void GetHome()
    {
        try {
            this.lblstatus.Text = "Home取得中...";
            var tweets = _twitter.statuses_home_timeline(100);
            AddNode(tweets);
            this.lblstatus.Text = "Home取得完了";
        } catch (TwitterAPIException ex) {
            this.lblstatus.Text = Utilization.SubTwitterAPIExceptionStr(ex);
        }
    }
    //----------------------------------------------------------------------------------------
    #endregion

    //----------------------------------------------------------------------------------------
    #region -AddNode ノード追加
    //----------------------------------------------------------------------------------------
    private void AddNode(IEnumerable<TwitData> tweets)
    {
        bool isTop = (GtkScrolledWindow.Vadjustment.Value == 0);
        foreach (TwitData t in tweets) {
            if (_tweetList.ContainsKey(t.StatusID)) {
                continue;
            }
            NodeTweetData node = new NodeTweetData(t.UserScreenName, t.UserName, t.Time.ToString("MM/dd HH:mm:ss"), t.Source, t.Text);
            _tweetList.Add(t.StatusID, node);
            int pos = _tweetList.IndexOfKey(t.StatusID);
            _store.AddNode(node, pos);
        }
        if (isTop) {
            GtkScrolledWindow.Vadjustment.Value = 0;
        }
    }
    //----------------------------------------------------------------------------------------
    #endregion

    // UserStream
    CancellationTokenSource _userStreamCancellationTS;

    protected void OnActionStartUStActivated(object sender, System.EventArgs e)
    {
        ActionStartUSt.Sensitive = false;
        StartUserStream(false);
        ActionStopUSt.Sensitive = true;
    }

    protected void OnActionStopUStActivated(object sender, System.EventArgs e)
    {
        ActionStopUSt.Sensitive = false;
        EndUserStream();
    }

    private void StartUserStream(bool all_replies)
    {
        lblUserStream.Text = "UserStream開始中...";
        _userStreamCancellationTS = _twitter.userstream_user(all_replies, UserStreamTransaction, UserStreamEndEvent);
        GetHome();
        lblUserStream.Text = "UserStream利用中";
    }

    private void EndUserStream()
    {
        lblUserStream.Text = "UserStream終了中";
        _userStreamCancellationTS.Cancel();
    }

    private void UserStreamEndEvent()
    {
        lblUserStream.Text = "";
        ActionStartUSt.Sensitive = true;
    }

    private void UserStreamTransaction(UserStreamItemType type, object data)
    {
            try {
                switch (type) {
                    case UserStreamItemType.friendlist:
                        break;
                    case UserStreamItemType.status: {
                            TwitData twitdata = (TwitData)data;
                            AddNode(twitdata.AsEnumerable());
                            }
                        break;
                    case UserStreamItemType.directmessage: {
                            //
                        }
                        break;
                    case UserStreamItemType.status_delete: {
                            //
                        }
                        break;
                    case UserStreamItemType.directmessage_delete: {
                            //
                        }
                        break;
                    case UserStreamItemType.eventdata: {
                            #region EventData表示処理
                            //-----------------------------------------------------------
                            UserStreamEventData d = (UserStreamEventData)data;
                            switch (d.Type) {
                                case UserStreamEventType.favorite:
                                        //
                                    break;
                                case UserStreamEventType.unfavorite:
                                        //
                                    break;
                                case UserStreamEventType.follow:
                                        //
                                    break;
                                case UserStreamEventType.block:
                                        //
                                    break;
                                case UserStreamEventType.unblock:
                                        //
                                    break;
                                case UserStreamEventType.list_member_added:
                                        //
                                    break;
                                case UserStreamEventType.list_member_removed:
                                        //
                                    break;
                                case UserStreamEventType.list_created:
                                        //
                                    break;
                                case UserStreamEventType.list_updated:
                                        //
                                    break;
                                case UserStreamEventType.list_destroyed:
                                        //
                                    break;
                                case UserStreamEventType.list_user_subscribed:
                                        //
                                    break;
                                case UserStreamEventType.list_user_unsubscribed:
                                        //
                                    break;
                                case UserStreamEventType.user_update:
                                        //
                                    break;
                            }
                            //-----------------------------------------------------------
                            #endregion
                        }
                        break;
                    case UserStreamItemType.tracklimit:
                        break;
                }
            }
            catch (InvalidOperationException) { }
            catch (Exception ex) {
            }
        }

    //----------------------------------------------------------------------------------------
    #region -BackGroundThread(別スレッド)
    //----------------------------------------------------------------------------------------
    private bool _profileRenew_IsForce = false;
    private DateTime _profile_Standard;
    private readonly TimeSpan PROFILE_TIMESPAN = new TimeSpan(0, 10, 0);
    private bool _homeRenew_IsForce = false;
    private DateTime _home_Standard;
    private readonly TimeSpan HOME_TIMESPAN = new TimeSpan(0, 2, 0);
    // otherThread
    private void BackGroundThread()
    {
        while (true) {
            if (_isAuthenticated) {
                DateTime now = DateTime.Now;
                if (_profileRenew_IsForce
                 || (now.Subtract(_profile_Standard).CompareTo(PROFILE_TIMESPAN) > 0)) {
                    GetProfile();
                    _profileRenew_IsForce = false;
                    _profile_Standard = now;
                }
                if (_homeRenew_IsForce
                 || (now.Subtract(_home_Standard).CompareTo(HOME_TIMESPAN) > 0)) {
                    GetHome();
                    _homeRenew_IsForce = false;
                    _home_Standard = now;
                }
            }
            Thread.Sleep(50);
        }
    }
    //----------------------------------------------------------------------------------------
    #endregion
    //----------------------------------------------------------------------------------------

    private void a()
    {
    }

}

/// <summary>
/// Node tweet data.
/// </summary>
public class NodeTweetData : TreeNode
{
    public NodeTweetData(string screen_name, string username, string datetime, string client, string text)
    {
        ScreenName = screen_name;
        UserName = username;
        DateTime = datetime;
        ClientName = client;
        Text = text;
    }
    
    [TreeNodeValue(Column=0)]
    public string ScreenName { get; set; }

    [TreeNodeValue(Column=1)]
    public string UserName { get; set; }

    [TreeNodeValue(Column=2)]
    public string DateTime { get; set; }

    [TreeNodeValue(Column=3)]
    public string ClientName { get; set; }

    [TreeNodeValue(Column=4)]
    public string Text { get; set; }
}