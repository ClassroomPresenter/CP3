using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Diagnostics;

namespace UW.ClassroomPresenter.Network {
    public class PacketTypes {
        #region PacketIds

        public const int MessageId                                      = 1;

        public const int GroupInformationMessageId                      = 2;
        public const int ParticipantGroupAddedMessageId                 = 3;
        public const int ParticipantGroupRemovedMessageId               = 4;

        public const int RoleMessageId                                  = 5;
        public const int InstructorMessageId                            = 6;
        public const int StudentMessageId                               = 7;
        public const int PublicMessageId                                = 8;

        public const int PresentationMessageId                          = 9;
        public const int InstructorCurrentPresentationChangedMessageId  = 10;
        public const int PresentationInformationMessageId               = 11;
        public const int PresentationEndedMessageId                     = 12;

        public const int DeckTraversalMessageId                         = 13;
        public const int InstructorCurrentDeckTraversalChangedMessageId = 14;
        public const int SlideDeckTraversalMessageId                    = 15;
        public const int DeckTraversalRemovedFromPresentationMessageId  = 16;

        public const int ExecuteScriptMessageId                         = 17;

        public const int SynchronizationMessageId                       = 18;
        public const int SyncBeginMessageId                             = 19;
        public const int SyncPingMessageId                              = 20;
        public const int SyncPongMessageId                              = 21;

        public const int DeckMessageId                                  = 22;
        public const int DeckInformationMessageId                       = 23;

        public const int SheetMessageId                                 = 24;
        public const int SheetRemovedMessageId                          = 25;
        public const int ImageSheetMessageId                            = 26;
        public const int TextSheetMessageId                             = 27;
        public const int InkSheetMessageId                              = 28;
        public const int InkSheetInformationMessageId                   = 29;
        public const int InkSheetStrokesAddedMessageId                  = 30;
        public const int InkSheetStrokesDeletingMessageId               = 31;
        public const int RealTimeInkSheetMessageId                      = 32;
        public const int RealTimeInkSheetInformationMessageId           = 33;

        public const int RealTimeInkSheetDataMessageId                  = 34;
        public const int RealTimeInkSheetPacketsMessageId               = 35;
        public const int RealTimeInkSheetStylusUpMessageId              = 36;
        public const int RealTimeInkSheetStylusDownMessageId            = 37;

        public const int SlideMessageId                                 = 38;
        public const int SlideInformationMessageId                      = 39;
        public const int SlideDeletedMessageId                          = 40;
        public const int StudentSubmissionSlideMessageId                = 41;
        public const int StudentSubmissionSlideInformationMessageId     = 42;

        public const int SubmissionStatusMessageId                      = 43;

        public const int TableOfContentsEntryMessageId                  = 44;

        public const int TableOfContentsEntryRemovedMessageId           = 45;

        public const int DeckSlideContentMessageId                      = 50;

        public const int BroadcastMessageId                             = 46;
        public const int ChunkId                                        = 47;
        public const int TCPHandshakeMessageId                          = 48;
        public const int TCPHeartbeatMessageId                          = 49;

        public const int VersionRequestMessageId                        = 51;
        public const int VersionResponseMessageId                       = 52;

        public const int QuickPollMessageId                             = 53;
        public const int QuickPollInformationMessageId                  = 54;
        public const int QuickPollResultMessageId                       = 55;
        public const int QuickPollResultInformationMessageId            = 56;
        public const int QuickPollResultRemovedMessageId                = 57;

        public const int QuickPollSheetMessageId                        = 58;

        public const int BoolId                                         = 100;
        public const int ByteId                                         = 101;
        public const int Integer32Id                                    = 102;
        public const int FloatId                                        = 103;
        public const int Integer64Id                                    = 104;
        public const int LongId                                         = 105;
        public const int ULongId                                        = 106;
        public const int ByteArrayId                                    = 107;
        public const int IntArrayId                                     = 108;
        public const int StringId                                       = 109;
        public const int IPEndPointId                                   = 110;
        public const int ColorId                                        = 111;
        public const int RectangleId                                    = 112;
        public const int GuidId                                         = 113;
        public const int GroupId                                        = 114;
        public const int SubmissionStatusModelId                        = 115;
        public const int ByteArrayClassId                               = 116;
        public const int LocalIdId                                      = 117;
        public const int DrawingAttributesSerializerId                  = 118;
        public const int TabletPropertyDescriptionInformationId         = 119;
        public const int TabletPropertyDescriptionCollectionInformationId = 120;
        public const int QuickPollModelId                               = 121;
        public const int QuickPollResultModelId                         = 122;

        #endregion

        public static IGenericSerializable DecodeMessage( Messages.Message parent, SerializedPacket p ) {
            switch( p.Type ) {
                case MessageId:
                    throw new Exception( "Cannot deserialize an abstract class!" );
                case GroupInformationMessageId:
                    throw new Exception( "Cannot deserialize an abstract class!" );
                case ParticipantGroupAddedMessageId:
                    return new Messages.Network.ParticipantGroupAddedMessage( parent, p );
                case ParticipantGroupRemovedMessageId:
                    return new Messages.Network.ParticipantGroupRemovedMessage( parent, p );

                case RoleMessageId:
                    throw new Exception( "Cannot deserialize an abstract class!" );
                case InstructorMessageId:
                    return new Messages.Network.InstructorMessage( parent, p );
                case StudentMessageId:
                    return new Messages.Network.StudentMessage( parent, p );
                case PublicMessageId:
                    return new Messages.Network.PublicMessage( parent, p );

                case PresentationMessageId:
                    throw new Exception( "Cannot deserialize an abstract class!" );
                case InstructorCurrentPresentationChangedMessageId:
                    return new Messages.Network.InstructorCurrentPresentationChangedMessage( parent, p );
                case PresentationInformationMessageId:
                    return new Messages.Presentation.PresentationInformationMessage( parent, p );
                case PresentationEndedMessageId:
                    return new Messages.Presentation.PresentationEndedMessage( parent, p );

                case DeckTraversalMessageId:
                    throw new Exception( "Cannot deserialize an abstract class!" );
                case InstructorCurrentDeckTraversalChangedMessageId:
                    return new Messages.Network.InstructorCurrentDeckTraversalChangedMessage( parent, p );
                case SlideDeckTraversalMessageId:
                    return new Messages.Presentation.SlideDeckTraversalMessage( parent, p );
                case DeckTraversalRemovedFromPresentationMessageId:
                    return new Messages.Presentation.DeckTraversalRemovedFromPresentationMessage( parent, p );

                case ExecuteScriptMessageId:
                    return new Messages.ExecuteScriptMessage( parent, p );

                case SynchronizationMessageId:
                    throw new Exception( "Cannot deserialize an abstract class!" );
                case SyncBeginMessageId:
                    return new Messages.SyncBeginMessage( parent, p );
                case SyncPingMessageId:
                    return new Messages.SyncPingMessage( parent, p );
                case SyncPongMessageId:
                    return new Messages.SyncPongMessage( parent, p );

                case DeckMessageId:
                    throw new Exception( "Cannot deserialize an abstract class!" );
                case DeckInformationMessageId:
                    return new Messages.Presentation.DeckInformationMessage( parent, p );

                case SheetMessageId:
                    throw new Exception( "Cannot deserialize an abstract class!" );
                case SheetRemovedMessageId:
                    return new Messages.Presentation.SheetRemovedMessage( parent, p );
                case ImageSheetMessageId:
                    return new Messages.Presentation.ImageSheetMessage( parent, p );
                case TextSheetMessageId:
                    return new Messages.Presentation.TextSheetMessage( parent, p );
                case InkSheetMessageId:
                    throw new Exception( "Cannot deserialize an abstract class!" );
                case InkSheetInformationMessageId:
                    return new Messages.Presentation.InkSheetInformationMessage( parent, p );
                case InkSheetStrokesAddedMessageId:
                    return new Messages.Presentation.InkSheetStrokesAddedMessage( parent, p );
                case InkSheetStrokesDeletingMessageId:
                    return new Messages.Presentation.InkSheetStrokesDeletingMessage( parent, p );
                case RealTimeInkSheetMessageId:
                    throw new Exception( "Cannot deserialize an abstract class!" );
                case RealTimeInkSheetInformationMessageId:
                    return new Messages.Presentation.RealTimeInkSheetInformationMessage( parent, p );

                case RealTimeInkSheetDataMessageId:
                    throw new Exception( "Cannot deserialize an abstract class!" );
                case RealTimeInkSheetPacketsMessageId:
                    return new Messages.Presentation.RealTimeInkSheetPacketsMessage( parent, p );
                case RealTimeInkSheetStylusUpMessageId:
                    return new Messages.Presentation.RealTimeInkSheetStylusUpMessage( parent, p );
                case RealTimeInkSheetStylusDownMessageId:
                    return new Messages.Presentation.RealTimeInkSheetStylusDownMessage( parent, p );

                case SlideMessageId:
                    throw new Exception( "Cannot deserialize an abstract class!" );
                case SlideInformationMessageId:
                    return new Messages.Presentation.SlideInformationMessage( parent, p );
                case SlideDeletedMessageId:
                    return new Messages.Presentation.SlideDeletedMessage( parent, p );
                case StudentSubmissionSlideMessageId:
                    throw new Exception( "Cannot deserialize an abstract class!" );
                case StudentSubmissionSlideInformationMessageId:
                    return new Messages.Presentation.StudentSubmissionSlideInformationMessage( parent, p );

                case SubmissionStatusMessageId:
                    return new Messages.Presentation.SubmissionStatusMessage( parent, p );

                case TableOfContentsEntryMessageId:
                    return new Messages.Presentation.TableOfContentsEntryMessage( parent, p );

                case TableOfContentsEntryRemovedMessageId:
                    return new Messages.Presentation.TableOfContentsEntryRemovedMessage( parent, p );

                case DeckSlideContentMessageId:
                    return new Messages.Presentation.DeckSlideContentMessage( parent, p );

                case BroadcastMessageId:
                    return new Broadcast.BroadcastMessage( p );
                case ChunkId:
                    return new Chunking.Chunk( p );
                case TCPHandshakeMessageId:
                    return new TCP.TCPHandshakeMessage( p );
                case TCPHeartbeatMessageId:
                    return new TCP.TCPHeartbeatMessage( p );

                case VersionRequestMessageId:
                    return new Messages.Network.VersionRequestMessage( parent, p );
                case VersionResponseMessageId:
                    return new Messages.Network.VersionResponseMessage( parent, p );

                case QuickPollMessageId:
                    throw new Exception( "Cannot deserialize an abstract class!" );
                case QuickPollInformationMessageId:
                    return new Messages.Presentation.QuickPollInformationMessage( parent, p );
                case QuickPollResultMessageId:
                    throw new Exception( "Cannot deserialize an abstract class!" );
                case QuickPollResultInformationMessageId:
                    return new Messages.Presentation.QuickPollResultInformationMessage( parent, p );
                case QuickPollResultRemovedMessageId:
                    return new Messages.Presentation.QuickPollResultRemovedMessage( parent, p );

                case QuickPollSheetMessageId:
                    return new Messages.Presentation.QuickPollSheetMessage( parent, p );

                default:
                    throw new Exception( "Unknown Packet ID: " + p.Type );
            }
        }

        public static string GetName( int type ) {
            if( type < 0 )
                type = -type;
            switch( type ) {
                case MessageId:
                    return "ABSTRACT: Message\n";
                case GroupInformationMessageId:
                    return "ABSTRACT: GroupInformationMessage\n";
                case ParticipantGroupAddedMessageId:
                    return "ParticipantGroupAddedMessage\n";
                case ParticipantGroupRemovedMessageId:
                    return "ParticipantGroupRemovedMessage\n";

                case RoleMessageId:
                    return "ABSTRACT: RoleMessageId\n";
                case InstructorMessageId:
                    return "InstructorMessage\n";
                case StudentMessageId:
                    return "StudentMessage\n";
                case PublicMessageId:
                    return "PublicMessage\n";

                case PresentationMessageId:
                    return "ABSTRACT: PresentationMessage\n";
                case InstructorCurrentPresentationChangedMessageId:
                    return "InstructorCurrentPresentationChangedMessage\n";
                case PresentationInformationMessageId:
                    return "PresentationInformationMessage\n";
                case PresentationEndedMessageId:
                    return "PresentationEndedMessage\n";

                case DeckTraversalMessageId:
                    return "ABSTRACT: DeckTraversalMessage\n";
                case InstructorCurrentDeckTraversalChangedMessageId:
                    return "InstructorCurrentDeckTraversalChangedMessage\n";
                case SlideDeckTraversalMessageId:
                    return "SlideDeckTraversalMessage\n";
                case DeckTraversalRemovedFromPresentationMessageId:
                    return "DeckTraversalRemovedFromPresentationMessage\n";

                case ExecuteScriptMessageId:
                    return "ExecuteScriptMessage\n";

                case SynchronizationMessageId:
                    return "ABSTRACT: SynchronizationMessage\n";
                case SyncBeginMessageId:
                    return "SyncBeginMessage\n";
                case SyncPingMessageId:
                    return "SyncPingMessage\n";
                case SyncPongMessageId:
                    return "SyncPongMessage\n";

                case DeckMessageId:
                    return "ABSTRACT: DeckMessage\n";
                case DeckInformationMessageId:
                    return "DeckInformationMessage\n";

                case SheetMessageId:
                    return "ABSTRACT: SheetMessage\n";
                case SheetRemovedMessageId:
                    return "SheetRemovedMessage\n";
                case ImageSheetMessageId:
                    return "ImageSheetMessage\n";
                case TextSheetMessageId:
                    return "TextSheetMessage\n";
                case InkSheetMessageId:
                    return "ABSTRACT: InkSheetMessage\n";
                case InkSheetInformationMessageId:
                    return "InkSheetInformationMessage\n";
                case InkSheetStrokesAddedMessageId:
                    return "InkSheetStrokesAddedMessage\n";
                case InkSheetStrokesDeletingMessageId:
                    return "InkSheetStrokesDeletingMessage\n";
                case RealTimeInkSheetMessageId:
                    return "ABSTRACT: RealTimeInkSheetMessage\n";
                case RealTimeInkSheetInformationMessageId:
                    return "RealTimeInkSheetInformationMessage\n";

                case RealTimeInkSheetDataMessageId:
                    return "ABSTRACT: RealTimeInkSheetDataMessage\n";
                case RealTimeInkSheetPacketsMessageId:
                    return "RealTimeInkSheetPacketsMessage\n";
                case RealTimeInkSheetStylusUpMessageId:
                    return "RealTimeInkSheetStylusUpMessage\n";
                case RealTimeInkSheetStylusDownMessageId:
                    return "RealTimeInkSheetStylusDownMessage\n";

                case SlideMessageId:
                    return "ABSTRACT: SlideMessage\n";
                case SlideInformationMessageId:
                    return "SlideInformationMessage\n";
                case SlideDeletedMessageId:
                    return "SlideDeletedMessage\n";
                case StudentSubmissionSlideMessageId:
                    return "ABSTRACT: StudentSubmissionSlideMessage\n";
                case StudentSubmissionSlideInformationMessageId:
                    return "StudentSubmissionSlideInformationMessage\n";

                case SubmissionStatusMessageId:
                    return "SubmissionStatusMessage\n";

                case TableOfContentsEntryMessageId:
                    return "TableOfContentsEntryMessage\n";

                case TableOfContentsEntryRemovedMessageId:
                    return "TableOfContentsEntryRemovedMessage\n";

                case DeckSlideContentMessageId:
                    return "DeckSlideContentMessage\n";

                case BroadcastMessageId:
                    return "BroadcastMessage\n";
                case ChunkId:
                    return "Chunk\n";
                case TCPHandshakeMessageId:
                    return "TCPHandshakeMessage\n";
                case TCPHeartbeatMessageId:
                    return "TCPHeartbeatMessage\n";

                case VersionRequestMessageId:
                    return "VersionRequestMessage\n";
                case VersionResponseMessageId:
                    return "VersionResponseMessage\n";

                case QuickPollMessageId:
                    return "ABSTRACT: QuickPollMessage\n";
                case QuickPollInformationMessageId:
                    return "QuickPollInformation\n";
                case QuickPollResultMessageId:
                    return "ABSTRACT: QuickPollResultMessage\n";
                case QuickPollResultInformationMessageId:
                    return "QuickPollResultInformationMessage\n";
                case QuickPollResultRemovedMessageId:
                    return "QuickPollResultRemovedMessage\n";

                case QuickPollSheetMessageId:
                    return "QuickPollSheetMessage\n";

                case BoolId:
                    return "\tBool\n";
                case ByteId:
                    return "\tByte\n";
                case Integer32Id:
                    return "\tInt32\n";
                case FloatId:
                    return "\tFloat\n";
                case Integer64Id:
                    return "\tInt64\n";
                case LongId:
                    return "\tLong\n";
                case ULongId:
                    return "\tULong\n";
                case ByteArrayId:
                    return "\tByteArray\n";
                case IntArrayId:
                    return "\tIntArray\n";
                case StringId:
                    return "\tString\n";
                case IPEndPointId:
                    return "\tIPEndPoint\n";
                case ColorId:
                    return "\tColor\n";
                case RectangleId:
                    return "\tRectangle\n";
                case GuidId:
                    return "\tGuid\n";
                case GroupId:
                    return "\tGroup\n";
                case SubmissionStatusModelId:
                    return "\tSubmissionStatusModel\n";
                case ByteArrayClassId:
                    return "\tByteArrayClass\n";
                case LocalIdId:
                    return "\tLocalId\n";
                case DrawingAttributesSerializerId:
                    return "\tDrawingAttributesSerializer\n";
                case TabletPropertyDescriptionInformationId:
                    return "\tTabletPropertyDescriptionInformation\n";
                case TabletPropertyDescriptionCollectionInformationId:
                    return "\tTabletPropertyDescriptionCollectionInformation\n";
                case QuickPollModelId:
                    return "\tQuickPollModel\n";
                case QuickPollResultModelId:
                    return "\tQuickPollResultModel\n";
                default:
                    return "ERROR!!!\n";
            }
        }

/*
        public static IGenericSerializable DecodeMessage( Messages.Message parent, Stream input ) {
            int type = Util.DeserializeInt( input );
            switch( type ) {
                case MessageId:
                    throw new Exception( "Cannot deserialize an abstract class!" );
                case GroupInformationMessageId:
                    throw new Exception( "Cannot deserialize an abstract class!" );
                case ParticipantGroupAddedMessageId:
                    return new Messages.Network.ParticipantGroupAddedMessage( parent, input );
                case ParticipantGroupRemovedMessageId:
                    return new Messages.Network.ParticipantGroupRemovedMessage( parent, input );

                case RoleMessageId:
                    throw new Exception( "Cannot deserialize an abstract class!" );
                case InstructorMessageId:
                    return new Messages.Network.InstructorMessage( parent, input );
                case StudentMessageId:
                    return new Messages.Network.StudentMessage( parent, input );
                case PublicMessageId:
                    return new Messages.Network.PublicMessage( parent, input );

                case PresentationMessageId:
                    throw new Exception( "Cannot deserialize an abstract class!" );
                case InstructorCurrentPresentationChangedMessageId:
                    return new Messages.Network.InstructorCurrentPresentationChangedMessage( parent, input );
                case PresentationInformationMessageId:
                    return new Messages.Presentation.PresentationInformationMessage( parent, input );
                case PresentationEndedMessageId:
                    return new Messages.Presentation.PresentationEndedMessage( parent, input );

                case DeckTraversalMessageId:
                    throw new Exception( "Cannot deserialize an abstract class!" );
                case InstructorCurrentDeckTraversalChangedMessageId:
                    return new Messages.Network.InstructorCurrentDeckTraversalChangedMessage( parent, input );
                case SlideDeckTraversalMessageId:
                    return new Messages.Presentation.SlideDeckTraversalMessage( parent, input );
                case DeckTraversalRemovedFromPresentationMessageId:
                    return new Messages.Presentation.DeckTraversalRemovedFromPresentationMessage( parent, input );

                case ExecuteScriptMessageId:
                    return new Messages.ExecuteScriptMessage( parent, input );

                case SynchronizationMessageId:
                    throw new Exception( "Cannot deserialize an abstract class!" );
                case SyncBeginMessageId:
                    return new Messages.SyncBeginMessage( parent, input );
                case SyncPingMessageId:
                    return new Messages.SyncPingMessage( parent, input );
                case SyncPongMessageId:
                    return new Messages.SyncPongMessage( parent, input );

                case DeckMessageId:
                    throw new Exception( "Cannot deserialize an abstract class!" );
                case DeckInformationMessageId:
                    return new Messages.Presentation.DeckInformationMessage( parent, input );

                case SheetMessageId:
                    throw new Exception( "Cannot deserialize an abstract class!" );
                case SheetRemovedMessageId:
                    return new Messages.Presentation.SheetRemovedMessage( parent, input );
                case ImageSheetMessageId:
                    return new Messages.Presentation.ImageSheetMessage( parent, input );
                case TextSheetMessageId:
                    return new Messages.Presentation.TextSheetMessage( parent, input );
                case InkSheetMessageId:
                    throw new Exception( "Cannot deserialize an abstract class!" );
                case InkSheetInformationMessageId:
                    return new Messages.Presentation.InkSheetInformationMessage( parent, input );
                case InkSheetStrokesAddedMessageId:
                    return new Messages.Presentation.InkSheetStrokesAddedMessage( parent, input );
                case InkSheetStrokesDeletingMessageId:
                    return new Messages.Presentation.InkSheetStrokesDeletingMessage( parent, input );
                case RealTimeInkSheetMessageId:
                    throw new Exception( "Cannot deserialize an abstract class!" );
                case RealTimeInkSheetInformationMessageId:
                    return new Messages.Presentation.RealTimeInkSheetInformationMessage( parent, input );

                case RealTimeInkSheetDataMessageId:
                    throw new Exception( "Cannot deserialize an abstract class!" );
                case RealTimeInkSheetPacketsMessageId:
                    return new Messages.Presentation.RealTimeInkSheetPacketsMessage( parent, input );
                case RealTimeInkSheetStylusUpMessageId:
                    return new Messages.Presentation.RealTimeInkSheetStylusUpMessage( parent, input );
                case RealTimeInkSheetStylusDownMessageId:
                    return new Messages.Presentation.RealTimeInkSheetStylusDownMessage( parent, input );

                case SlideMessageId:
                    throw new Exception( "Cannot deserialize an abstract class!" );
                case SlideInformationMessageId:
                    return new Messages.Presentation.SlideInformationMessage( parent, input );
                case SlideDeletedMessageId:
                    return new Messages.Presentation.SlideDeletedMessage( parent, input );
                case StudentSubmissionSlideMessageId:
                    throw new Exception( "Cannot deserialize an abstract class!" );
                case StudentSubmissionSlideInformationMessageId:
                    return new Messages.Presentation.StudentSubmissionSlideInformationMessage( parent, input );

                case SubmissionStatusMessageId:
                    return new Messages.Presentation.SubmissionStatusMessage( parent, input );

                case TableOfContentsEntryMessageId:
                    return new Messages.Presentation.TableOfContentsEntryMessage( parent, input );

                case TableOfContentsEntryRemovedMessageId:
                    return new Messages.Presentation.TableOfContentsEntryRemovedMessage( parent, input );

                case DeckSlideContentMessageId:
                    return new Messages.Presentation.DeckSlideContentMessage( parent, input );

                case BroadcastMessageId:
                    return new Broadcast.BroadcastMessage( input );
                case ChunkId:
                    return new Chunking.Chunk( input );
                case TCPHandshakeMessageId:
                    return new TCP.TCPHandshakeMessage( input );
                case TCPHeartbeatMessageId:
                    return new TCP.TCPHeartbeatMessage();
                default:
                    throw new Exception( "Unknown Packet ID" );
            }
        }
*/
    }
}
