using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Linq.Flickr;
using LinqExtender;
using System.Windows;

namespace FlickrDeDupr
{
    /// <summary>
    /// Interaction logic for WindowMain.xaml
    /// </summary>
    public partial class WindowMain
    {
        private ObservableCollection<Photo> _duplicatePhotos = new ObservableCollection<Photo>(); 
        private readonly FlickrContext _context;

        ///<summary>
        /// Main window for the application
        ///</summary>
        public WindowMain()
        {
            InitializeComponent();

            // Setup the context for our Flickr queries
            _context = new FlickrContext();
            _context.Photos.OnError += PhotosOnError;
        }

        public ObservableCollection<Photo> DuplicatePhotos
        {
            get { return _duplicatePhotos; }
// ReSharper disable UnusedMember.Local
            private set { _duplicatePhotos = value; }
// ReSharper restore UnusedMember.Local
        }

        private void FindDuplicates()
        {
            Dispatcher.Invoke( DispatcherPriority.ApplicationIdle, new Action( ForceWaitCursor ) );
            Dispatcher.Invoke( DispatcherPriority.ApplicationIdle, new Action( FindDuplicatesWork ) );
            Dispatcher.Invoke( DispatcherPriority.ApplicationIdle, new Action( SelectDuplicatePhotos ) );
            Dispatcher.Invoke( DispatcherPriority.ApplicationIdle, new Action( RestoreDefaultCursor ) );
            listViewResults.Focus();
        }

        private void FindDuplicatesWork()
        {
            DuplicatePhotos.Clear();

            int numberOfPhotosChecked = 0;
            int numberOfPhotos = 999;
            Photo previousPhoto = null;

            int lastPageChecked = 0;
            while ( numberOfPhotosChecked != numberOfPhotos )
            {
                var query = ( from photo in _context.Photos
                              where photo.ViewMode == ViewMode.Owner
                                    && photo.Extras == ( ExtrasOption.Date_Taken
                                                         | ExtrasOption.Views
                                                         | ExtrasOption.Date_Upload
                                                         | ExtrasOption.Tags )
                              orderby PhotoOrder.Date_Taken descending
                              select photo ).Take( 500 ).Skip( lastPageChecked );

                foreach ( var photo in query )
                {
                    numberOfPhotosChecked++;
                    if ( previousPhoto != null )
                    {
                        if ( photo.Title == previousPhoto.Title
                             && photo.TakeOn == previousPhoto.TakeOn )
                        {
                            if ( !DuplicatePhotos.Contains( previousPhoto ) )
                            {
                                DuplicatePhotos.Add( previousPhoto );
                            }
                            if ( !DuplicatePhotos.Contains( photo ) )
                            {
                                DuplicatePhotos.Add( photo );
                            }
                        }
                    }
                    previousPhoto = photo;
                }
                if ( previousPhoto != null )
                {
                    lastPageChecked = previousPhoto.SharedProperty.Page;
                    numberOfPhotos = previousPhoto.SharedProperty.Total;
                }
                else
                {
                    numberOfPhotos = 0;
                }
            }
        }

        private void SelectDuplicatePhotos()
        {
            if ( !DuplicatePhotos.Any() )
            {
                return;
            }
            Photo previousPhoto = null;
            for ( int i = 0; i < DuplicatePhotos.Count; i++ )
            {
                Photo photo = DuplicatePhotos[i];
                if ( previousPhoto != null )
                {
                    if ( photo.Title == previousPhoto.Title
                         && photo.TakeOn == previousPhoto.TakeOn )
                    {
                        var item = listViewResults.Items[i - 1];
                        listViewResults.SelectedItems.Add( item );
                    }
                }
                previousPhoto = photo;
            }
        }

        static void PhotosOnError(ProviderException ex)
        {
            MessageBox.Show(ex.Message);
        }

        private void ListBoxItem_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var item = sender as ListBoxItem;
            if (item == null || !item.IsSelected) return;

            var photo = listViewResults.SelectedItem as Photo;
            if (photo == null) return;

            System.Diagnostics.Process.Start(photo.WebUrl);
        }

        private void ButtonRefresh_Click(object sender, RoutedEventArgs e)
        {
            FindDuplicates();
        }

        private void RestoreDefaultCursor()
        {
            Cursor = null;
            ForceCursor = false;
        }

        private void ForceWaitCursor()
        {
            ForceCursor = true;
            Cursor = Cursors.Wait;
        }

        private void ButtonDelete_Click(object sender, RoutedEventArgs e)
        {
            Dispatcher.Invoke( DispatcherPriority.ApplicationIdle, new Action( ForceWaitCursor ) );
            int deletedPhotoCount = 0;

            if (listViewResults.SelectedItems.Count > 0)
            {
                // You can only remove photos from the current _context.Photos
                // So we need to ensure the duplicate photos are there
                var query = (from photo in _context.Photos
                             where photo.ViewMode == ViewMode.Owner
                                   &&
                                   photo.Extras == (ExtrasOption.Date_Taken 
                                                    | ExtrasOption.Views | ExtrasOption.Date_Upload 
                                                    | ExtrasOption.Tags)
                                   && DuplicatePhotos.Contains(photo)
                             select photo).Take(500).Skip(0);

                // Force the query to evaluate and load _context.Photos
#pragma warning disable 168
                Photo dummyPhoto = query.First();
#pragma warning restore 168

                foreach (Photo photo in listViewResults.SelectedItems)
                {
                    _context.Photos.Remove(photo);
                    deletedPhotoCount++;
                }
                _context.SubmitChanges();
                FindDuplicates();
            }
            Dispatcher.Invoke( DispatcherPriority.ApplicationIdle, new Action( RestoreDefaultCursor ) );
            if (deletedPhotoCount > 0)
            {
                string deletedMessage = "Deleted 1 photo.";
                if (deletedPhotoCount > 1)
                {
                    deletedMessage = string.Format("Deleted {0} photos.", deletedPhotoCount); 
                }
                MessageBox.Show(deletedMessage,"Photos Deleted");
            }
            else
            {
                MessageBox.Show("Make sure you select a photo/s to delete", "No photos deleted.");
            }
            Dispatcher.Invoke( DispatcherPriority.ApplicationIdle, new Action( RestoreDefaultCursor ) );
        }

        private void ButtonClearAuthCache_Click(object sender, RoutedEventArgs e)
        {
            _context.ClearToken();
        }
    }
}