namespace FlickrDeDupr
{
    using System;
    using System.Collections.ObjectModel;
    using System.Diagnostics;
    using System.Linq;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Input;
    using System.Windows.Threading;

    using Linq.Flickr;

    using LinqExtender;

    /// <summary>
    /// Interaction logic for WindowMain.xaml
    /// </summary>
    public partial class WindowMain
    {
        #region Fields

        private readonly FlickrContext context;

        private ObservableCollection<Photo> duplicatePhotos = new ObservableCollection<Photo>();

        #endregion

        #region Constructors and Destructors

        ///<summary>
        /// Main window for the application
        ///</summary>
        public WindowMain()
        {
            this.InitializeComponent();

            // Setup the context for our Flickr queries
            this.context = new FlickrContext();
            this.context.Photos.OnError += PhotosOnError;
        }

        #endregion

        #region Public Properties

        public ObservableCollection<Photo> DuplicatePhotos
        {
            get
            {
                return this.duplicatePhotos;
            }
            // ReSharper disable UnusedMember.Local
            private set
            {
                this.duplicatePhotos = value;
            }
            // ReSharper restore UnusedMember.Local
        }

        #endregion

        #region Methods

        private static void PhotosOnError(ProviderException ex)
        {
            MessageBox.Show(ex.Message);
        }

        private void ButtonClearAuthCache_OnClick(object sender, RoutedEventArgs e)
        {
            this.context.ClearToken();
        }

        private void ButtonDelete_OnClick(object sender, RoutedEventArgs e)
        {
            this.Dispatcher.Invoke(DispatcherPriority.ApplicationIdle, new Action(this.ForceWaitCursor));
            int deletedPhotoCount = 0;

            if (this.listViewResults.SelectedItems.Count > 0)
            {
                // You can only remove photos from the current _context.Photos
                // So we need to ensure the duplicate photos are there
                var query = (from photo in this.context.Photos
                    where
                        photo.ViewMode == ViewMode.Owner
                        && photo.Extras
                        == (ExtrasOption.Date_Taken | ExtrasOption.Views | ExtrasOption.Date_Upload | ExtrasOption.Tags)
                        && this.DuplicatePhotos.Contains(photo)
                    select photo).Take(500).Skip(0);

                // Force the query to evaluate and load _context.Photos
#pragma warning disable 168
                Photo dummyPhoto = query.First();
#pragma warning restore 168

                foreach (Photo photo in this.listViewResults.SelectedItems)
                {
                    this.context.Photos.Remove(photo);
                    deletedPhotoCount++;
                }
                this.context.SubmitChanges();
                this.FindDuplicates();
            }
            this.Dispatcher.Invoke(DispatcherPriority.ApplicationIdle, new Action(this.RestoreDefaultCursor));
            if (deletedPhotoCount > 0)
            {
                string deletedMessage = "Deleted 1 photo.";
                if (deletedPhotoCount > 1)
                {
                    deletedMessage = string.Format("Deleted {0} photos.", deletedPhotoCount);
                }
                MessageBox.Show(deletedMessage, "Photos Deleted");
            }
            else
            {
                MessageBox.Show("Make sure you select a photo/s to delete", "No photos deleted.");
            }
            this.Dispatcher.Invoke(DispatcherPriority.ApplicationIdle, new Action(this.RestoreDefaultCursor));
        }

        private void ButtonRefresh_OnClick(object sender, RoutedEventArgs e)
        {
            this.FindDuplicates();
        }

        private void FindDuplicates()
        {
            this.Dispatcher.Invoke(DispatcherPriority.ApplicationIdle, new Action(this.ForceWaitCursor));
            this.Dispatcher.Invoke(DispatcherPriority.ApplicationIdle, new Action(this.FindDuplicatesWork));
            this.Dispatcher.Invoke(DispatcherPriority.ApplicationIdle, new Action(this.SelectDuplicatePhotos));
            this.Dispatcher.Invoke(DispatcherPriority.ApplicationIdle, new Action(this.RestoreDefaultCursor));
            this.listViewResults.Focus();
        }

        private void FindDuplicatesWork()
        {
            this.DuplicatePhotos.Clear();

            int numberOfPhotosChecked = 0;
            int numberOfPhotos = 999;
            Photo previousPhoto = null;

            int lastPageChecked = 0;
            while (numberOfPhotosChecked != numberOfPhotos)
            {
                var query = (from photo in this.context.Photos
                    where
                        photo.ViewMode == ViewMode.Owner
                        && photo.Extras
                        == (ExtrasOption.Date_Taken | ExtrasOption.Views | ExtrasOption.Date_Upload | ExtrasOption.Tags)
                    orderby PhotoOrder.Date_Taken descending
                    select photo).Take(500).Skip(lastPageChecked);

                foreach (var photo in query)
                {
                    numberOfPhotosChecked++;
                    if (previousPhoto != null)
                    {
                        if (photo.Title == previousPhoto.Title && photo.TakeOn == previousPhoto.TakeOn)
                        {
                            if (!this.DuplicatePhotos.Contains(previousPhoto))
                            {
                                this.DuplicatePhotos.Add(previousPhoto);
                            }
                            if (!this.DuplicatePhotos.Contains(photo))
                            {
                                this.DuplicatePhotos.Add(photo);
                            }
                        }
                    }
                    previousPhoto = photo;
                }
                if (previousPhoto != null)
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

        private void ForceWaitCursor()
        {
            this.ForceCursor = true;
            this.Cursor = Cursors.Wait;
        }

        private void ListBoxItem_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var item = sender as ListBoxItem;
            if (item == null || !item.IsSelected)
            {
                return;
            }

            var photo = this.listViewResults.SelectedItem as Photo;
            if (photo == null)
            {
                return;
            }

            Process.Start(photo.WebUrl);
        }

        private void RestoreDefaultCursor()
        {
            this.Cursor = null;
            this.ForceCursor = false;
        }

        private void SelectDuplicatePhotos()
        {
            if (!this.DuplicatePhotos.Any())
            {
                return;
            }
            Photo previousPhoto = null;
            for (int i = 0; i < this.DuplicatePhotos.Count; i++)
            {
                Photo photo = this.DuplicatePhotos[i];
                if (previousPhoto != null)
                {
                    if (photo.Title == previousPhoto.Title && photo.TakeOn == previousPhoto.TakeOn)
                    {
                        var item = this.listViewResults.Items[i - 1];
                        this.listViewResults.SelectedItems.Add(item);
                    }
                }
                previousPhoto = photo;
            }
        }

        #endregion
    }
}