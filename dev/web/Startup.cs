using System;
using SkriftColorsDemo.Utils;
using Umbraco.Core;
using Umbraco.Core.Events;
using Umbraco.Core.Logging;
using Umbraco.Core.Models;
using Umbraco.Core.Services;

namespace SkriftColorsDemo.Web {
    
    public class Startup : ApplicationEventHandler {
        
        protected override void ApplicationStarted(UmbracoApplicationBase umbraco, ApplicationContext context) {

            // Used for extracting primary colors from media on save
            MediaService.Saving += MediaServiceOnSaving;

        }

        private void MediaServiceOnSaving(IMediaService sender, SaveEventArgs<IMedia> e) {

            // Loop through each media being saved (eg. if being uploaded)
            foreach (IMedia media in e.SavedEntities) {
                
                // Ignore non-images
                if (media.ContentType.Alias != "Image") continue;
                
                try {
                    SkybrudMediaUtils.CalculatePrimaryColors(media);
                } catch (Exception ex) {
                    LogHelper.Error<Startup>("MediaServiceOnSaving", ex);
                }
            
            }

        }
    
    }

}