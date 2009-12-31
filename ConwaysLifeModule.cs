/*
 * Copyright (c) Contributors http://github.com/aduffy70/ConwayGOL
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the ConwaysLife module nor the
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

namespace ConwaysLifeModule
{
    public class ConwaysLifeModule : IRegionModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
		List<SceneObjectGroup> m_prims = new List<SceneObjectGroup>();
		float m_xCenter = 128f; //inworld x coordinate for the 0,0,0 position
		float m_yCenter = 128f;  //inworld y coordinate for the 0,0,0 position
		float m_zCenter = 40f; //inworld z coordinate for the 0,0,0 position
		float m_aRadius = 20f; //overall torus radius
		float m_bRadius = 15f; //torus tube radius
		int m_xCells = 35;
		int m_yCells = 35;
		int[] m_cloudMatrix; // = new int[36 * 36];
        Color4 m_deadColor = new Color4(0f, 0f, 0f, 0.25f); //color for dead cells
        Color4 m_liveColor = new Color4(1.0f, 1f, 1f, 1.0f); //color for live cells
        int m_running = 0; //Keep track of whether the game is running
        int m_needsUpdate = 0; //Only schedule updates if there was a change
		Timer m_timer = new Timer(); //Timer to replace the region heartbeat
        bool m_enabled = false;
        int m_channel = 9;
        int m_cycleTime = 5000; //Time in milliseconds between cycles
		private Scene m_scene;

        #region IRegionModule interface

        public void Initialise(Scene scene, IConfigSource config)
        {
            IConfig conwayConfig = config.Configs["ConwayGOL"];
            if (conwayConfig != null)
            {
                m_enabled = conwayConfig.GetBoolean("enabled", false);
                m_cycleTime = conwayConfig.GetInt("cycle_time", 5) * 1000;
                m_channel = conwayConfig.GetInt("chat_channel", 9);
                m_xCells = conwayConfig.GetInt("x_cells", 36) - 1;
                m_yCells = conwayConfig.GetInt("y_cells", 36) - 1;
                m_xCenter = conwayConfig.GetFloat("x_position", 128);
                m_yCenter = conwayConfig.GetFloat("y_position", 128);
                m_zCenter = conwayConfig.GetFloat("z_position", 40);
                m_aRadius = conwayConfig.GetFloat("a_radius", 20);
                m_bRadius = conwayConfig.GetFloat("b_radius", 15);
            }
            if (m_enabled)
            {
                m_log.Info("[ConwaysLifeModule] Initializing...");
                m_scene = scene;
            }
        }

        public void PostInitialise()
        {
            if (m_enabled)
            {
                m_scene.EventManager.OnChatFromWorld += new EventManager.ChatFromWorldEvent(OnChat);
                m_scene.EventManager.OnChatFromClient += new EventManager.ChatFromClientEvent(OnChat);
                m_cloudMatrix = new int[(m_xCells + 1) * (m_yCells + 1)];
			    DoLifeModule(m_scene);
            }
        }

        public void Close()
        {
        }

        public string Name
        {
            get
            {
                return "ConwaysLifeModule";
            }
        }

        public bool IsSharedModule
        {
            get
            {
                return false;
            }
        }

        #endregion

        void DoLifeModule(Scene scene)
        {
            // We're going to place a torus of dead (white) cells in world
			float twoPi = 2f * (float)Math.PI;
			float uSpacing = twoPi / (m_xCells + 1);
			float vSpacing = twoPi / (m_yCells + 1);
			float uRadians = 0;
			float vRadians = 0;
			int counter = 0;
			for (int y=0; y<=m_yCells; y++)
            {
				for (int x=0; x<=m_xCells; x++)
                {
					float xPos = m_xCenter + ((m_aRadius + (m_bRadius * (float)Math.Cos(vRadians))) * (float)Math.Cos(uRadians));
					float yPos = m_yCenter + ((m_aRadius + (m_bRadius * (float)Math.Cos(vRadians))) * (float)Math.Sin(uRadians));
					float zPos = m_zCenter + (m_bRadius * (float)Math.Sin(vRadians));
					Vector3 pos = new Vector3(xPos, yPos, zPos);
					PrimitiveBaseShape prim = PrimitiveBaseShape.CreateSphere();
					prim.Textures = new Primitive.TextureEntry(new UUID("5748decc-f629-461c-9a36-a35a236fe36f"));
					SceneObjectGroup sog = new SceneObjectGroup(UUID.Zero, pos, prim);
					float size = 0.75f + (Math.Abs((m_xCells / 2f) - (float)x) / (m_xCells / 3f));
					sog.RootPart.Scale = new Vector3(size, size, size);
					Primitive.TextureEntry tex = sog.RootPart.Shape.Textures;
					m_cloudMatrix[counter] = 0;
					tex.DefaultTexture.RGBA = m_deadColor;
					sog.RootPart.UpdateTexture(tex);
					m_prims.Add(sog);
					vRadians = vRadians + vSpacing;
					counter++;
				}
				uRadians = uRadians + uSpacing;
			}
			//Add these objects to the list of managed objects
			//Place the objects visibly on the scene
			m_running = 0;
			foreach (SceneObjectGroup sogr in m_prims)
            {
				scene.AddNewSceneObject(sogr, false);
            }
		}

        void OnChat(Object sender, OSChatMessage chat)
        {
            if ((chat.Channel != m_channel) || (chat.Message.Length < 5))
			{
            	return;
            }
			else if (chat.Message.Substring(0,5) == "reset")
            { //Stop ticking and set all cells dead
				m_log.Info("[ConwaysLife] resetting...");
				m_timer.Stop();
				m_running = 0;
				for (int countprims = 0; countprims < ((m_xCells + 1) * (m_yCells + 1)); countprims ++)
                {
					Primitive.TextureEntry tex = m_prims[countprims].RootPart.Shape.Textures;
					if (tex.DefaultTexture.RGBA == m_liveColor)
                    {
						tex.DefaultTexture.RGBA = m_deadColor;
						m_prims[countprims].RootPart.UpdateTexture(tex);
					}
				}
			}
			else if ((chat.Message.Substring(0,5) == "start") && (m_running == 0))
            { //see which cells are alive and start ticking
				m_log.Info("[ConwaysLife] Starting...");
				for (int counter=0; counter< ((m_xCells + 1) * (m_yCells + 1)); counter++)
                {
					Primitive.TextureEntry tex = m_prims[counter].RootPart.Shape.Textures;
					if (tex.DefaultTexture.RGBA == m_liveColor)
                    {
						m_cloudMatrix[counter] = 1;
					}
					else
                    {
						m_cloudMatrix[counter] = 0;
					}
					counter++;
				}
				m_running = 1;
				m_timer.Elapsed += new ElapsedEventHandler(TimerEvent);
				m_timer.Interval = m_cycleTime;
				m_timer.Start();
			}
			else if ((chat.Message.Substring(0,4) == "stop") && (m_running == 1))
            { //stop ticking
				m_log.Info("[ConwaysLife] Stopping...");
				m_running = 0;
				m_timer.Stop();
			}
			else if ((chat.Message.Length > 6) && (m_running == 0))
            { //load an example pattern
				m_log.Info("[ConwaysLife] Setting starting pattern...");
				for (int countprims = 0; countprims < (36 * 36); countprims ++)
                {
					Primitive.TextureEntry tex = m_prims[countprims].RootPart.Shape.Textures;
					if (tex.DefaultTexture.RGBA == m_liveColor)
                    {
						tex.DefaultTexture.RGBA = m_deadColor;
						m_prims[countprims].RootPart.UpdateTexture(tex);
					}
				}
				//Glider
				SetLive(17, 26);
				SetLive(18, 26);
				SetLive(19, 26);
				SetLive(19, 27);
				SetLive(18, 28);
				//Glider
				SetLive(26, 17);
				SetLive(27, 17);
				SetLive(26, 18);
				SetLive(28, 18);
				SetLive(26, 19);
				//Glider
				SetLive(18, 8);
				SetLive(17, 9);
				SetLive(17, 10);
				SetLive(18, 10);
				SetLive(19, 10);
				//Glider
				SetLive(10, 17);
				SetLive(8, 18);
				SetLive(10, 18);
				SetLive(9, 19);
				SetLive(10, 19);
				//Cross
				SetLive(0, 0);
				SetLive(1, 0);
				SetLive(35, 0);
				//Cross
				SetLive(18, 17);
				SetLive(18, 18);
				SetLive(18, 19);
				//Cross
				SetLive(0, 17);
				SetLive(0, 18);
				SetLive(0, 19);
				//Cross
				SetLive(17, 0);
				SetLive(18, 0);
				SetLive(19, 0);
			}
		}

		void SetLive(int xPos, int yPos)
        {
			Primitive.TextureEntry tex = m_prims[yPos * (m_xCells + 1) + xPos].RootPart.Shape.Textures;
			m_cloudMatrix[yPos * (m_xCells + 1) + xPos] = 1;
			tex.DefaultTexture.RGBA = m_liveColor;
			m_prims[yPos * (m_xCells + 1) + xPos].RootPart.UpdateTexture(tex);
		}

		void TimerEvent(object source, ElapsedEventArgs e)
        {
			OnTick();
		}

		void OnTick()
        {
			Color4 setColor = new Color4();
    	    int rowAbove = new int();
        	int rowBelow = new int();
           	int colLeft = new int();
            int colRight = new int();
            int[] newValues = new int[(m_xCells + 1) * (m_yCells + 1)];
            int counter = 0;
            for (int y=0; y<=m_yCells; y++)
            {
				if (y == 0)
                {
					rowAbove = y + 1;
					rowBelow = m_yCells;
				}
				else if (y == m_yCells)
                {
					rowAbove = 0;
					rowBelow = y - 1;
				}
				else
                {
					rowAbove = y + 1;
					rowBelow = y - 1;
				}
            	for (int x=0; x<=m_xCells; x++)
                {
					if (x == 0)
                    {
						colRight = x + 1;
						colLeft = m_xCells;
					}
					else if (x == m_xCells)
                    {
						colRight = 0;
						colLeft = x - 1;
					}
					else
                    {
						colRight = x + 1;
						colLeft = x - 1;
					}
                   	if (m_cloudMatrix[counter] == 0)
                    {
                       	if ((m_cloudMatrix[rowBelow * (m_xCells + 1) + colLeft] + m_cloudMatrix[y * (m_xCells + 1) + colLeft] + m_cloudMatrix[rowAbove * (m_xCells + 1) + colLeft] + m_cloudMatrix[rowBelow * (m_xCells + 1) + x] + m_cloudMatrix[rowAbove * (m_xCells + 1) + x] + m_cloudMatrix[rowBelow * (m_xCells + 1) + colRight] + m_cloudMatrix[y * (m_xCells + 1) + colRight] + m_cloudMatrix[rowAbove * (m_xCells + 1) + colRight]) == 3)
                        {
                       		newValues[counter] = 1;
                       		setColor = m_liveColor;
							m_needsUpdate = 1;
                       	}
                       	else
                        {
                       		newValues[counter] = 0;
							m_needsUpdate = 0;
                       	}
                   	}
                   	else if (m_cloudMatrix[counter] == 1)
                    {
                       	if (((m_cloudMatrix[rowBelow * (m_xCells + 1) + colLeft] + m_cloudMatrix[y * (m_xCells + 1) + colLeft] + m_cloudMatrix[rowAbove * (m_xCells + 1) + colLeft] + m_cloudMatrix[rowBelow * (m_xCells + 1) + x] + m_cloudMatrix[rowAbove * (m_xCells + 1) + x] + m_cloudMatrix[rowBelow * (m_xCells + 1) + colRight] + m_cloudMatrix[y * (m_xCells + 1) + colRight] + m_cloudMatrix[rowAbove * (m_xCells + 1) + colRight]) == 2) || ((m_cloudMatrix[rowBelow * (m_xCells + 1) + colLeft] + m_cloudMatrix[y * (m_xCells + 1) + colLeft] + m_cloudMatrix[rowAbove * (m_xCells + 1) + colLeft] + m_cloudMatrix[rowBelow * (m_xCells + 1) + x] + m_cloudMatrix[rowAbove * (m_xCells + 1) + x] + m_cloudMatrix[rowBelow * (m_xCells + 1) + colRight] + m_cloudMatrix[y * (m_xCells + 1) + colRight] + m_cloudMatrix[rowAbove * (m_xCells + 1) + colRight]) == 3))
                        {
                       		newValues[counter] = 1;
							m_needsUpdate = 0;
                       	}
                       	else
                        {
                       		newValues[counter] = 0;
                       		setColor = m_deadColor;
							m_needsUpdate = 1;
                       	}
                   	}
                   	else
                    {
                       	m_log.Info("Value is out of range!");
                   	}
					if (m_needsUpdate == 1)
                    {
						Primitive.TextureEntry tex = m_prims[counter].RootPart.Shape.Textures;
						tex.DefaultTexture.RGBA = setColor;
                   		m_prims[counter].RootPart.UpdateTexture(tex);
               			m_prims[counter].ScheduleGroupForTerseUpdate();
					}
					counter++;
				}
			}
	        Array.Copy(newValues, m_cloudMatrix, (m_xCells + 1) * (m_yCells + 1));
        }
    }
}
