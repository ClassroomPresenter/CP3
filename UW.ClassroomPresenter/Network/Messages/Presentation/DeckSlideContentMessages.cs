// $Id: DeckSlideContentMessages.cs 1871 2009-05-26 19:29:56Z cmprince $

using System;
using System.Collections;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.Serialization;
using System.Threading;

using UW.ClassroomPresenter.Model.Presentation;

namespace UW.ClassroomPresenter.Network.Messages.Presentation {

    [Serializable]
    public class DeckSlideContentMessage : Message {
        /// <summary>
        /// The <see cref="ImageHashtable.ImageHashTableItem"/> serves as a <em>serializable</em> wrapper around an <see cref="Image"/>.
        /// </summary>
        public readonly ImageHashtable.ImageHashTableItem Content;

        public DeckSlideContentMessage(DeckModel deck, ByteArray hash) : base(hash) {
            using(Synchronizer.Lock(deck.SyncRoot)) {
                Image image = deck.GetSlideContent(hash);

                if(image == null)
                    throw new ArgumentException("The specified ByteArray does not map to slide content in the specified deck.", "hash");

                this.Target = this.Content = new ImageHashtable.ImageHashTableItem(hash, image);
                this.AddLocalRef( this.Target );
            }
        }

/*
        public DeckSlideContentMessage( DeckModel deck, ByteArray hash )
            : this( deck, hash, 320, 240, 32, System.Drawing.Imaging.ImageFormat.Png ) {
        }

        private bool Stub() {
            return true;
        }

        public DeckSlideContentMessage( DeckModel deck, ByteArray hash, int width, int height, int depth, System.Drawing.Imaging.ImageFormat fmt ) : base( hash ) {
            using( Synchronizer.Lock( deck.SyncRoot ) ) {
                Image image = deck.GetSlideContent( hash );

                if( image == null )
                    throw new ArgumentException( "The specified ByteArray does not map to slide content in the specified deck.", "hash" );

                Image.GetThumbnailImageAbort callback = new Image.GetThumbnailImageAbort( Stub );
                Image small = image.GetThumbnailImage( width, height, callback, System.IntPtr.Zero );
                string tempFile = System.IO.Path.GetTempFileName();
                small.Save( tempFile, fmt );
                byte[] smallData = System.IO.File.ReadAllBytes( tempFile );
                System.IO.MemoryStream ms = new System.IO.MemoryStream( smallData );
                Image newSmall = Image.FromStream( ms );

                // Cleanup Temp File
                this.Target = this.Content = new ImageHashtable.ImageHashTableItem( hash, newSmall );
                this.AddLocalRef( this.Target );
            }
        }
*/
        protected override bool UpdateTarget(ReceiveContext context) {
#if GENERIC_SERIALIZATION
            if( this.Content == null )
                return true;
#else
            Debug.Assert( this.Content != null );
#endif
            if(this.Content.image == null)
                throw new ArgumentException("The deserialized content image is null.", "Content");

            this.Target = this.Content;

            DeckModel deck = this.Parent != null ? this.Parent.Target as DeckModel : null;
            if(deck != null) {
                using(Synchronizer.Lock(deck.SyncRoot)) {
                    // Note: If the deck already has content with this hash, nothing will happen.
                    deck.AddSlideContent(this.Content.key, this.Content.image);
                }
            }

            return true;
        }

        #region IGenericSerializable

        public override SerializedPacket Serialize() {
            SerializedPacket p = base.Serialize();
            return p;
        }

        public DeckSlideContentMessage( Message parent, SerializedPacket p ) : base( parent, p ) {
            this.Content = null;
        }

        public override int GetClassId() {
            return PacketTypes.DeckSlideContentMessageId;
        }

        #endregion
    }
}
