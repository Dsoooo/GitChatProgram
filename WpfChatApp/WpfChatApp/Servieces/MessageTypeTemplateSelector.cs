using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows;
using WpfChatApp.Model;

namespace WpfChatApp
{
    /// <summary>
    /// 메시지 양식에 따른 return
    /// </summary>
    public class MessageTypeTemplateSelector : DataTemplateSelector
    {
        public DataTemplate TextTemplate { get; set; }
        public DataTemplate ImageTemplate { get; set; }
        public DataTemplate VideoTemplate { get; set; }
        public DataTemplate DocumentTemplate { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            var message = item as ChatMessage;
            if (message != null)
            {
                switch (message.ContentType)
                {
                    case MessageType.Image:
                        return ImageTemplate;
                    case MessageType.Video:
                        return VideoTemplate;
                    case MessageType.Document:
                        return DocumentTemplate;
                    default:
                        return TextTemplate;
                }
            }
            return base.SelectTemplate(item, container);
        }
    }
}
