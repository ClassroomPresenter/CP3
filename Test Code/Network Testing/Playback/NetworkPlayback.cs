// $Id: NetworkPlayback.cs 774 2005-09-21 20:22:33Z pediddle $

using System;
using System.Collections;
using UW.ClassroomPresenter.Test.Network.Common;
using UW.ClassroomPresenter.Network.Chunking;
using UW.ClassroomPresenter.Network.Messages;

namespace UW.ClassroomPresenter.Test.Network.Playback {
    /// <summary>
    /// Summary description for NetworkPlayback.
    /// </summary>
    [Serializable]
    public class NetworkPlayback {
        private ArrayList chunkEvents = new ArrayList();
        private ArrayList messageEvents = new ArrayList();
        public enum NetworkPlaybackModeEnum {
            Chunk,
            Message
        }

        public NetworkPlayback(string filename) {
            ChunkAssembler assembler = new ChunkAssembler();
            NetworkArchiver archiver = new NetworkArchiver();
            archiver.OpenArchive(filename);

            while (archiver.HasMoreEvents()) {
                //Get the current event                
                NetworkEvent ne = archiver.GetNextEvent();
                if (ne != null) {
                    //Add it to the chunkEvents (which contains everything)
                    this.chunkEvents.Add(ne);
                    if (ne is NetworkChunkEvent) {
                        NetworkChunkEvent nce = (NetworkChunkEvent)ne;
                        //Add it to the chunk assembler
                        Message m = assembler.Add(nce.chunk);
                        if (m != null) {
                            //We have a complete message!
                            NetworkMessageEvent nme = new NetworkMessageEvent(m, ne.timeIndex, ne.source);
                            this.messageEvents.Add(nme);
                        }
                    } else if (ne is NetworkMessageEvent) {
                        this.messageEvents.Add(((NetworkMessageEvent)ne).message);
                    } else {
                        //Skip
                    }
                }
            }
            archiver.CloseArchive();
        }

        public string[] GetEvents(NetworkPlaybackModeEnum mode) {
            string[] toReturn;
            if (mode == NetworkPlaybackModeEnum.Chunk) {
                toReturn = new string[this.chunkEvents.Count];
                for (int i = 0; i < this.chunkEvents.Count; i++) {
                    toReturn[i] = this.chunkEvents[i].ToString();
                }
            } else {
                toReturn = new string[this.messageEvents.Count];
                for (int i = 0; i < this.messageEvents.Count; i++) {
                    toReturn[i] = ((NetworkMessageEvent)this.messageEvents[i]).message.ToString();
                }
            }
            return toReturn;
        }

        public void Delete(ArrayList al, NetworkPlaybackModeEnum mode) {
            //Since the ArrayList items are already presorted, remove them from the back forward
            if (mode == NetworkPlaybackModeEnum.Chunk) {
                for (int i = al.Count - 1; i >= 0; i--) {
                    this.chunkEvents.RemoveAt((int)al[i]);
                }
            } else {
                for (int i = al.Count - 1; i >= 0; i--) {
                    this.messageEvents.RemoveAt((int)al[i]);
                }
            }  
        }

        public void MoveUp(ArrayList al, NetworkPlaybackModeEnum mode) {
            //We move these ones in ascending index order
            if (mode == NetworkPlaybackModeEnum.Chunk) {
                for (int i = 0; i < al.Count; i++) {
                    int index = (int)al[i];
                    if (index > 0) {
                        this.chunkEvents.Reverse(index - 1, 2);
                    }
                }
            } else {
                for (int i = 0; i < al.Count; i++) {
                    int index = (int)al[i];
                    if (index > 0) {
                        this.messageEvents.Reverse(index - 1, 2);
                    }
                }
            }
        }

        public void MoveDown(ArrayList al, NetworkPlaybackModeEnum mode) {
            //We move these ones in descending index order
            if (mode == NetworkPlaybackModeEnum.Chunk) {
                for (int i = al.Count - 1; i >= 0; i--) {
                    int index = (int)al[i];
                    if (index < this.chunkEvents.Count - 1) {
                        this.chunkEvents.Reverse(index, 2);
                    }
                }
            } else {
                for (int i = al.Count - 1; i >= 0; i--) {
                    int index = (int)al[i];
                    if (index < this.messageEvents.Count - 1) {
                        this.messageEvents.Reverse(index, 2);
                    }
                }
            }
        }

        public NetworkEvent GetEvent(int index, NetworkPlaybackModeEnum mode) {
            if (mode == NetworkPlaybackModeEnum.Chunk) {
                if (index < this.chunkEvents.Count && index >= 0) {
                    return (NetworkEvent)this.chunkEvents[index];
                } else {
                    return null;
                }
            } else {
                if (index < this.messageEvents.Count && index >= 0) {
                    return (NetworkEvent)this.messageEvents[index];
                } else {
                    return null;
                }
            }
        }
    }
}
