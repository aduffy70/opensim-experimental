/*
 * Copyright (c) Contributors http://github.com/aduffy70/Coverview
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the Coverview Module nor the
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

using log4net;
using Nini.Config;
using OpenMetaverse;

using OpenSim.Framework;
using OpenSim.Region.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

namespace Coverview
{
    public class CoverviewModule : IRegionModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        //Configuration settings
        int chatChannel = 7;                      //Set the channel to use for commands
		int slideCount = 100;                     //Set the number of slides here
		float[] offset = new float[200];          //MUST be set to (2 * slidecount)
		int[] position = new int[100];  		  //MUST be set to slidecount
		int[] updatedPosition = new int[100];     //MUST be set to slidecount
		int rootPosition = 83;                    //The center (x) position for the displayed slide
		int yPosition = 12;                       //Set the y position on the sim
		int zPosition = 23;                       //Set the altitude of the slides
		Vector3 size = new Vector3(0.1f, 4f, 3f); //Set the dimensions of the slides
        float spacing = 0.5f;                     //Set the space between the slides
		int displayed = new int();
        int current;
		Scene m_scene;
		List<SceneObjectGroup> prims = new List<SceneObjectGroup>();


        #region IRegionModule interface

        public void Initialise(Scene scene, IConfigSource config)
        {
            m_log.Info("[Coverview] Initializing...");
            m_scene = scene;
        }

        public void PostInitialise()
        {
            m_scene.EventManager.OnChatFromWorld += new EventManager.ChatFromWorldEvent(OnChat);
            m_scene.EventManager.OnChatFromClient += new EventManager.ChatFromClientEvent(OnChat);
			DoCoverview(m_scene);
        }

        public void Close()
        {
        }

        public string Name
        {
            get { return "Coverview Module"; }
        }

        public bool IsSharedModule
        {
            get { return false; }
        }

        #endregion


        void DoCoverview(Scene scene)
        {
            // We're going to place objects in world
            displayed = slideCount - 1;
            current = slideCount - 1;
			for (int x=0;x<=(2*displayed);x++)
            {
                if (x< displayed)
                {
                    position[x] = x;
                    updatedPosition[x] = x;
					offset[x] = (x - (displayed + 10f)) * spacing;
                }
                if (x == displayed)
                {
                    position[x] = x;
                    updatedPosition[x] = x;
                    offset[x] = 0.0f;
                }
                if (x > displayed)
                {
                    offset[x] = (x - (displayed - 10f)) * spacing;
                }
            }

            //Place prims in the region
            for (int x=0; x<=displayed; x++)
                {
                Vector3 pos = new Vector3(rootPosition + offset[position[x]], yPosition, zPosition);
                SceneObjectGroup sog = new SceneObjectGroup(UUID.Zero, pos, PrimitiveBaseShape.CreateBox());
                if (x==displayed)
                {
                    sog.UpdateGroupRotationR(new Quaternion(0,0,1,1));
                }
                sog.RootPart.Scale = size;
                prims.Add(sog);
            }

            // Now make them visible
            foreach (SceneObjectGroup sogr in prims)
            {
                scene.AddNewSceneObject(sogr, false);
            }
        }

        void OnChat(Object sender, OSChatMessage chat)
        {
			if (chat.Channel != chatChannel)
				return; //The message isn't for this module
			else
            {
                SceneObjectGroup[] moveOrder = new SceneObjectGroup[slideCount];
                string message = chat.Message;
                int wanted;
			    if (message == "+")
                {
					wanted = current + 1;
                }
				else if (message == "-")
                {
					wanted = current - 1;
                }
				else
                {
                    try //Make sure the message is an integer
                    {
                        wanted = Convert.ToInt32(message);
                    }
                    catch
                    {
                        m_log.Debug("[Coverview] Invalid message.  Only '+', '-', or a slide number are accepted.");
                        wanted = current;
                    }
                }
                if (wanted < 0)
                {
		    	    wanted = 0;
                }
			    if (wanted > slideCount - 1)
                {
				    wanted = slideCount - 1;
                }
			    m_log.Debug("[Coverview] Getting Slide " + wanted);
                int slideNumber = 0;
			    foreach (SceneObjectGroup sog in prims)
                {
                    moveOrder[slideNumber] = sog;
                    slideNumber++;
                }
                if (position[wanted] == displayed)
                {
                    //Do nothing - you already have the slide you want displayed
                }
                else if (position[wanted] > displayed)
                {
                    for (int x=0; x<=displayed; x++)
                    {
                        updatedPosition[x] = (displayed - position[wanted]) + position[x];
                        moveOrder[x].AbsolutePosition = new Vector3(rootPosition + offset[updatedPosition[x]], yPosition, zPosition);
                        if (updatedPosition[x] == displayed)
                        {
                            moveOrder[x].UpdateGroupRotationR(new Quaternion(0, 0, 1, 1));
                        }
                        else if (updatedPosition[x] < displayed)
                        {
                            moveOrder[x].UpdateGroupRotationR(new Quaternion(0, 0, 0, 0));
                        }
                        else
                        {
                            moveOrder[x].UpdateGroupRotationR(new Quaternion(0, 0, 1, 0));
                        }
                        moveOrder[x].ScheduleGroupForTerseUpdate();
                    }
                    Array.Copy(updatedPosition, position, slideCount);
                }
                else if (position[wanted] < displayed)
                {
                    for (int x=displayed; x>=0; x--)
                    {
                        updatedPosition[x] = (displayed - position[wanted]) + position[x];
                        moveOrder[x].AbsolutePosition = new Vector3(rootPosition + offset[updatedPosition[x]], yPosition, zPosition);
                        if (updatedPosition[x] == displayed)
                        {
                            moveOrder[x].UpdateGroupRotationR(new Quaternion(0, 0, 1, 1));
                        }
                        else if (updatedPosition[x] < displayed)
                        {
                            moveOrder[x].UpdateGroupRotationR(new Quaternion(0, 0, 0, 0));
                        }
                        else
                        {
                            moveOrder[x].UpdateGroupRotationR(new Quaternion(0, 0, 1, 0));
                        }
                        moveOrder[x].ScheduleGroupForTerseUpdate();
                    }
                    Array.Copy(updatedPosition, position, slideCount);
                }
                current = wanted;
            }
        }
    }
}
