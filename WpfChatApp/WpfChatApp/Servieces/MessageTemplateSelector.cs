using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows;
using WpfChatApp.Model;
using WpfChatApp.Servieces;

namespace WpfChatApp
{
    /// <summary>
    /// 날짜 표시용인데 상대방/나 메시지 표시용인지 Template 선택
    /// </summary>
    public class MessageTemplateSelector : DataTemplateSelector
    {
        public DataTemplate DateSeparatorTemplate { get; set; }     // 전체 Row 날짜 표시
        public DataTemplate MessageContainerTemplate { get; set; }  // 상대방/나 구분용
        public DataTemplate OutChkTemplate { get; set; }            // 퇴장 메시지용 템플릿


        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            if (item is ChatMessage msg)
            {
                switch (msg.ContentType)
                {
                    case MessageType.DateSeparator:
                        return DateSeparatorTemplate;

                    case MessageType.OutChk:
                        return OutChkTemplate;

                    default:
                        return MessageContainerTemplate;
                }
            }


            return base.SelectTemplate(item, container);
        }
    }

}
