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
 *     * Neither the name of the Visit-Logger Module nor the
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
        //Module will fail at startup if these are not specified in the VisitLogger.ini file.
		string m_twitterUserName = "Default";
        string m_twitterPassword = "password";
        Twitter m_twit;
        int m_blockTime = 3600;
        int m_maxComments = 20;
        int m_commentChannel = 15;
        Dictionary<string, DateTime> m_recentVisits = new Dictionary<string, DateTime>();
        int m_commentsToday = 0;
        Timer m_timer = new Timer();
        private Scene m_scene;
        bool m_enabled = false;

        #region IRegionModule interface

        public void Initialise(Scene scene, IConfigSource config) {
            IConfig visitLoggerConfig = config.Configs["VisitLogger"];
            if (visitLoggerConfig != null)
            {
                m_enabled = visitLoggerConfig.GetBoolean("enabled", false);
                m_twitterUserName = visitLoggerConfig.GetString("your_twitter_username", "Default");
                m_twitterPassword = visitLoggerConfig.GetString("your_twitter_password", "password");
                m_blockTime = visitLoggerConfig.GetInt("block_time", 3600);
                m_maxComments = visitLoggerConfig.GetInt("maximum_comments", 20);
                m_commentChannel = visitLoggerConfig.GetInt("comment_channel", 15);
            }
            if (m_enabled)
            {
                m_scene = scene;
            }
        }

        public void PostInitialise() {
            if (m_enabled)
            {
                m_twit = new Twitter(m_twitterUserName, m_twitterPassword);
                m_scene.EventManager.OnMakeRootAgent += new EventManager.OnMakeRootAgentDelegate(OnVisit);
                m_scene.EventManager.OnChatFromClient += new EventManager.ChatFromClientEvent(OnChat);
                m_timer.Elapsed += new ElapsedEventHandler(TimerEvent);
                m_timer.Interval = 86400000;
                m_timer.Start();
            }
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
            string visitorName = presence.Firstname + "_" + presence.Lastname;
            DateTime dt = DateTime.Now;
            if (m_recentVisits.ContainsKey(visitorName)) {
                if (DateTime.Now.Subtract(m_recentVisits[visitorName]).TotalSeconds > m_blockTime) {
                    m_twit.Status.Update("Repeat visitor: " + presence.Firstname + " " + presence.Lastname + " - " + String.Format("{0:f}", dt));
                    m_recentVisits[visitorName] = DateTime.Now;
                }
                else {
                    m_recentVisits[visitorName] = DateTime.Now;
                }
            }
            else {
                m_twit.Status.Update("New Visitor: " + presence.Firstname + " " + presence.Lastname + " - " + String.Format("{0:f}", dt));
                m_recentVisits.Add(visitorName, DateTime.Now);
            }
        }

        void OnChat(Object sender, OSChatMessage chat) {
            if (chat.Channel != m_commentChannel)
                return;
            else if (m_commentsToday <= m_maxComments) {
                string senderName = m_scene.GetUserName(chat.SenderUUID);
                string firstInitial = senderName.Substring(0,1);
                string lastInitial = senderName.Split(' ')[1].Substring(0,1);
                m_twit.Status.Update(firstInitial + lastInitial + ":" + chat.Message);
                IDialogModule dialogmod = m_scene.RequestModuleInterface<IDialogModule>();
                if (dialogmod != null) {
                    dialogmod.SendGeneralAlert("Thanks for the feedback!");
                }
                m_commentsToday++;
            }
            else  {
                IDialogModule dialogmod = m_scene.RequestModuleInterface<IDialogModule>();
                if (dialogmod != null)
                    dialogmod.SendGeneralAlert("Too many comments today.  Message not sent.");
            }
        }

        void TimerEvent(object source, ElapsedEventArgs e) {
            m_commentsToday = 0;
        }
    }
}
