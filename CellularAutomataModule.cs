/*
* Copyright (c) Contributors http://github.com/aduffy70/CellularAutomata
* See CONTRIBUTORS.TXT for a full list of copyright holders.
*
* Redistribution and use in source and binary forms, with or without
* modification, are permitted provided that the following conditions are met:
* * Redistributions of source code must retain the above copyright
* notice, this list of conditions and the following disclaimer.
* * Redistributions in binary form must reproduce the above copyright
* notice, this list of conditions and the following disclaimer in the
* documentation and/or other materials provided with the distribution.
* * Neither the name of the CellularAutomata module nor the
* names of its contributors may be used to endorse or promote products
* derived from this software without specific prior written permission.
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

namespace CellularAutomataModule
    {
    public class CellularAutomataModule : IRegionModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
		List<SceneObjectGroup> m_prims = new List<SceneObjectGroup>();
		int m_xStartPos = 45; //inworld x coordinate for the 0,0 position
		int m_yStartPos = 120;  //inworld y coordinate for the 0,0 position
		int m_zStartPos = 21;  //inworld z height for the grid
		int m_xCells = 16;
		int m_yCells = 16;
		float m_offset = 0.25f;
		float[] m_matrix = new float[16 * 16];
		//float[] m_newMatrix = new float[16 * 16];
        private Scene m_scene;
        Timer m_timer = new Timer(); //Timer to replace the region heartbeat
        Random m_random = new Random();

        #region IRegionModule interface

        public void Initialise(Scene scene, IConfigSource config)
        {
            m_scene = scene;
        }

        public void PostInitialise()
        {
            m_scene.EventManager.OnChatFromWorld += new EventManager.ChatFromWorldEvent(OnChat);
            m_scene.EventManager.OnChatFromClient += new EventManager.ChatFromClientEvent(OnChat);
            InitializeMatrix(m_scene);
        }

        public void Close()
        {
        }

        public string Name
        {
            get
            {
                return "CellularAutomataModule";
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

        void InitializeMatrix(Scene scene)
        {
            // We're going to place a grid of objects in world
			for (int x=0; x<m_xCells; x++)
            {
				for (int y=0; y<m_yCells; y++)
                {
					Vector3 pos = new Vector3(m_xStartPos + x, m_yStartPos + y, m_zStartPos);
					PrimitiveBaseShape prim = PrimitiveBaseShape.CreateBox();
					prim.Textures = new Primitive.TextureEntry(new UUID("5748decc-f629-461c-9a36-a35a221fe21f"));
					SceneObjectGroup sog = new SceneObjectGroup(UUID.Zero, pos, prim);
					sog.RootPart.Scale = new Vector3(0.9f, 0.9f, 0.1f);
					Primitive.TextureEntry tex = sog.RootPart.Shape.Textures;
					float randomValue = (float)m_random.NextDouble();
					Color4 texcolor = new Color4(randomValue, randomValue, randomValue, 1.0f);
					m_matrix[y * 16 + x] = randomValue;
					tex.DefaultTexture.RGBA = texcolor;
					sog.RootPart.UpdateTexture(tex);
					m_prims.Add(sog);
				}
			}
			//Add these objects to the list of managed objects
			//Place the objects visibly on the scene
			foreach (SceneObjectGroup sogr in m_prims)
            {
				scene.AddNewSceneObject(sogr, false);
            }
            //Start the timer
            m_timer.Elapsed += new ElapsedEventHandler(OnTimer);
            m_timer.Interval = 20000;
            m_timer.Start();
		}

        void OnChat(Object sender, OSChatMessage chat)
        {
            if ((chat.Channel != 4) || (chat.Message.Length < 5))
            {
				return;
            }
			else
            {
				if (chat.Message.Substring(0,5) == "reset")
                {
					if (chat.Message.Length > 6)
                    {
						m_offset = float.Parse(chat.Message.Substring(6));
					}
					for (int x=0; x<m_xCells; x++)
                    {
						for (int y=0; y<m_yCells; y++)
                        {
                            int index = y * 16 + x;
							float randomValue = (float)m_random.NextDouble();
							Color4 texcolor = new Color4(randomValue, randomValue, randomValue, 1.0f);
							m_matrix[index] = randomValue;
							Primitive.TextureEntry tex = m_prims[index].RootPart.Shape.Textures;
							tex.DefaultTexture.RGBA = texcolor;
                            m_prims[index].RootPart.UpdateTexture(tex);
                            m_prims[index].ScheduleGroupForTerseUpdate();
                        }
                    }
                }
            }
        }


		void OnTimer(object source, ElapsedEventArgs e)
        {
            UpdateMatrix();
        }

        void UpdateMatrix()
        {
            float[] newMatrix = new float[16 * 16];
            int rowabove;
            int rowbelow;
            int colleft;
            int colright;
            int xMaxIndex = m_xCells - 1;
            int yMaxIndex = m_yCells - 1;
            for (int x=0; x<m_xCells; x++)
            {
				if (x == 0)
                {
					colright = x + 1;
					colleft = xMaxIndex;
				}
				else if (x == xMaxIndex)
                {
					colright = 0;
					colleft = x - 1;
				}
				else
                {
					colright = x + 1;
					colleft = x - 1;
				}
				for (int y=0; y<m_yCells; y++)
                {
                    if (y == 0)
                    {
						rowabove = y + 1;
						rowbelow = yMaxIndex;
					}
					else if (y == yMaxIndex)
                    {
						rowabove = 0;
						rowbelow = y - 1;
					}
					else
                    {
						rowabove = y + 1;
						rowbelow = y - 1;
					}
                    int index = y * 16 + x;
                    float neighboraverage = ((m_matrix[rowbelow * 16 + colleft] +
                                              m_matrix[y * 16 + colleft] +
                                              m_matrix[rowabove * 16 + colleft] +
                                              m_matrix[rowbelow * 16 + x] +
                                              m_matrix[rowabove * 16 + x] +
                                              m_matrix[rowbelow * 16 + colright] +
                                              m_matrix[y * 16 + colright] +
                                              m_matrix[rowabove * 16 + colright] +
                                              m_matrix[index]
                                             ) / 9);
					float newCellValue = (neighboraverage + m_offset) % 1.0f;
                    newMatrix[index] = newCellValue;
					Primitive.TextureEntry tex = m_prims[index].RootPart.Shape.Textures;
					Color4 texcolor = new Color4(newCellValue, newCellValue, newCellValue, 1.0f);
					tex.DefaultTexture.RGBA = texcolor;
                    m_prims[index].RootPart.UpdateTexture(tex);
                    m_prims[index].ScheduleGroupForTerseUpdate();
				}
			}
	        Array.Copy(newMatrix, m_matrix, 16 * 16);
        }
    }
}
