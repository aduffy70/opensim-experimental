/*
 * Copyright (c) Contributors http://github.com/aduffy70/Visit-Logger
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the Parallel Selves Chat Bridge nor the
 *       names of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

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
	//Specify a twitter address, password, and URL
		Twitter twit = new Twitter("YourTwitterLogin", "yourtwitterpassword");
        string twitteraddress = "twitter.com/YourTwitterURL";
    //If an avatar re-visits within this many seconds, do not tweet.  (This helps prevent abuse and excessive tweets)
		int blocktime = 3600;
	//If we get more comments than this in 24 hours assume it is griefing.  (More abuse prevention)
        int maxcomments = 20;
    //Channel to listen for comments.  All chat on this channel will be tweeted.
        int commentchannel = 15;
        
        Dictionary<string, DateTime> recentvisits = new Dictionary<string, DateTime>();
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
            if (chat.Channel != commentchannel)
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
