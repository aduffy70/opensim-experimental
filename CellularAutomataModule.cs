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

namespace CellularAutomataModule {
    public class CellularAutomataModule : IRegionModule {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
		List<SceneObjectGroup> prims = new List<SceneObjectGroup>();
		int xstartpos = 130; //inworld x coordinate for the 0,0 position
		int ystartpos = 60;  //inworld y coordinate for the 0,0 position
		int zstartpos = 21;  //inworld z height for the grid
		int xcells = 15;
		int ycells = 15;
		float maxcover = 1.0f;
		float offset = 0.25f;
		float[] cloudmatrix = new float[16 * 16];
		float[] newvalues = new float[16 * 16];
        private Scene m_scene;
        Timer mytimer = new Timer(); //Timer to replace the region heartbeat

        #region IRegionModule interface

        public void Initialise(Scene scene, IConfigSource config) {
            m_scene = scene;
        }

        public void PostInitialise() {
            m_scene.EventManager.OnChatFromWorld += new EventManager.ChatFromWorldEvent(OnChat);
            m_scene.EventManager.OnChatFromClient += new EventManager.ChatFromClientEvent(OnChat);
			DoCloudModule(m_scene);
        }

        public void Close(){
        }

        public string Name{
            get { return "CellularAutomataModule"; }
        }

        public bool IsSharedModule {
            get { return false; }
        }

        #endregion

        void DoCloudModule(Scene scene) {
            // We're going to place a grid of objects in world
			Random RandomClass = new Random();
			for (int x=0; x<=xcells; x++) {
				for (int y=0; y<=ycells; y++) {
					Vector3 pos = new Vector3(xstartpos + x, ystartpos + y, zstartpos);
					PrimitiveBaseShape prim = PrimitiveBaseShape.CreateBox();
					prim.Textures = new Primitive.TextureEntry(new UUID("5748decc-f629-461c-9a36-a35a221fe21f"));
					SceneObjectGroup sog = new SceneObjectGroup(UUID.Zero, pos, prim);
					sog.RootPart.Scale = new Vector3(0.9f, 0.9f, 0.1f);
					Primitive.TextureEntry tex = sog.RootPart.Shape.Textures;
					float Randomvalue = (float)RandomClass.NextDouble();
					Color4 texcolor;
					texcolor = new Color4(Randomvalue, Randomvalue, Randomvalue, 1.0f);
					cloudmatrix[y * 16 + x] = Randomvalue;
					tex.DefaultTexture.RGBA = texcolor;
					sog.RootPart.UpdateTexture(tex);
					prims.Add(sog);
				}
			}
			//Add these objects to the list of managed objects
			//Place the objects visibly on the scene
			foreach (SceneObjectGroup sogr in prims)
				scene.AddNewSceneObject(sogr, false);
            //Start the timer
            mytimer.Elapsed += new ElapsedEventHandler(TimerEvent);
            mytimer.Interval = 20000;
            mytimer.Start();
		}

        void OnChat(Object sender, OSChatMessage chat) {
            if ((chat.Channel != 4) || (chat.Message.Length < 5))
				return;
			else {
				if (chat.Message.Substring(0,5) == "reset") {
					if (chat.Message.Length > 6)
						offset = float.Parse(chat.Message.Substring(6));
					Random RandomClass = new Random();
                    Color4 texcolor;
					int counter = 0;
					for (int x=0; x<=xcells; x++) {
						for (int y=0; y<=ycells; y++){
							float Randomvalue = (float)RandomClass.NextDouble();
							texcolor = new Color4(Randomvalue, Randomvalue, Randomvalue, 1.0f);
							cloudmatrix[y * 16 + x] = Randomvalue;
							cloudmatrix[y * 16 + x] *= maxcover;
							Primitive.TextureEntry tex = prims[counter].RootPart.Shape.Textures;
							tex.DefaultTexture.RGBA = texcolor;
                            prims[counter].RootPart.UpdateTexture(tex);
                            prims[counter].ScheduleGroupForTerseUpdate();
                            counter++;
                        }
                    }
                }
            }
        }


		void TimerEvent(object source, ElapsedEventArgs e) {
	        Color4 texcolor;
            int rowabove = new int();
            int rowbelow = new int();
            int colleft = new int();
            int colright = new int();
            int counter = 0;
            for (int x=0; x<=xcells; x++) {
				if (x == 0) {
					colright = x + 1;
					colleft = xcells;
				}
				else if (x == xcells) {
					colright = 0;
					colleft = x - 1;
				}
				else {
					colright = x + 1;
					colleft = x - 1;
				}
				for (int y=0; y<=ycells; y++) {
                    if (y == 0) {
						rowabove = y + 1;
						rowbelow = ycells;
					}
					else if (y == ycells) {
						rowabove = 0;
						rowbelow = y - 1;
					}
					else {
						rowabove = y + 1;
						rowbelow = y - 1;
					}
                    float neighboraverage = (((cloudmatrix[rowbelow * 16 + colleft] + cloudmatrix[y * 16 + colleft] + cloudmatrix[rowabove * 16 + colleft] + cloudmatrix[rowbelow * 16 + x] + cloudmatrix[rowabove * 16 + x] + cloudmatrix[rowbelow * 16 + colright] + cloudmatrix[y * 16 + colright] + cloudmatrix[rowabove * 16 + colright] + cloudmatrix[y * 16 + x]) / 9) / maxcover) + offset;
					newvalues[y * 16 + x] = neighboraverage % 1.0f;
					newvalues[y * 16 + x] *= maxcover;
					Primitive.TextureEntry tex = prims[counter].RootPart.Shape.Textures;
					texcolor = new Color4(newvalues[y * 16 + x], newvalues[y * 16 + x], newvalues[y * 16 + x], 1.0f);
					tex.DefaultTexture.RGBA = texcolor;
                    prims[counter].RootPart.UpdateTexture(tex);
                    prims[counter].ScheduleGroupForTerseUpdate();
					counter++;
				}
			}
	        Array.Copy(newvalues, cloudmatrix, 16 * 16);
        }
    }
}

