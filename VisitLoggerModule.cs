//An opensim region module to send a twitter "tweet" update announcing each visitor.
//Visitors can also record comments on channel 15.
//Aaron M. Duffy
//September 2009

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Timers;

using log4net;
using Nini.Config;
using OpenMetaverse;

using OpenSim.Framework;
using OpenSim.Region.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using Twitterizer.Framework;

namespace VisitLoggerModule {
    public class VisitLoggerModule : IRegionModule {
		Twitter twit = new Twitter("YourTwitterLogin", "yourtwitterpassword");
        string twitteraddress = "twitter.com/YourTwitterURL";
        Dictionary<string, DateTime> recentvisits = new Dictionary<string, DateTime>();
        int blocktime = 3600; //If an avatar re-visits within this many seconds, do not tweet
        int maxcomments = 20; //If we get more comments than this in 24 hours it is probably griefing
        int commentstoday = 0;
        Timer mytimer = new Timer();
        private Scene m_scene;

        #region IRegionModule interface

        public void Initialise(Scene scene, IConfigSource config) {
            m_scene = scene;
        }

        public void PostInitialise() {
            m_scene.EventManager.OnMakeRootAgent += new EventManager.OnMakeRootAgentDelegate(OnVisit);
            m_scene.EventManager.OnChatFromClient += new EventManager.ChatFromClientEvent(OnChat);
            mytimer.Elapsed += new ElapsedEventHandler(TimerEvent);
            mytimer.Interval = 86400000;
            mytimer.Start();
        }

        public void Close(){
        }

        public string Name{
            get { return "VisitLoggerModule"; }
        }

        public bool IsSharedModule {
            get { return false; }
        }

        #endregion

        void OnVisit(ScenePresence presence) {
            string visitorname = presence.Firstname + "_" + presence.Lastname;
            if (recentvisits.ContainsKey(visitorname)) {
                if (DateTime.Now.Subtract(recentvisits[visitorname]).TotalSeconds > blocktime) {
                    twit.Status.Update("Welcome " + presence.Firstname + " " + presence.Lastname + "!");
                    recentvisits[visitorname] = DateTime.Now;
                }
                else {
                    recentvisits[visitorname] = DateTime.Now;
                }
            }
            else {
                twit.Status.Update("Welcome " + presence.Firstname + " " + presence.Lastname + "!");
                recentvisits.Add(visitorname, DateTime.Now);
            }
        }

        void OnChat(Object sender, OSChatMessage chat) {
            if (chat.Channel != 15)
                return;
            else if (commentstoday <= maxcomments) {
                string sendername = m_scene.CommsManager.UserProfileCacheService.GetUserDetails(chat.SenderUUID).UserProfile.Name;
                //There must be a better way to get the avatar's name, but chat.Name returns null...
                string firstinitial = sendername.Substring(0,1);
                string lastinitial = sendername.Split(' ')[1].Substring(0,1);
                twit.Status.Update(firstinitial + lastinitial + ":" + chat.Message);
                IDialogModule dialogmod = m_scene.RequestModuleInterface<IDialogModule>();
                if (dialogmod != null) {
                    dialogmod.SendGeneralAlert("Thanks for the feedback!  View comments at " + twitteraddress);
                    commentstoday++;
                }
            }
            else  {
                IDialogModule dialogmod = m_scene.RequestModuleInterface<IDialogModule>();
                if (dialogmod != null)
                    dialogmod.SendGeneralAlert("Too many comments today.  Message not sent.");
            }
        }

        void TimerEvent(object source, ElapsedEventArgs e) {
            commentstoday = 0;
        }
    }
}
