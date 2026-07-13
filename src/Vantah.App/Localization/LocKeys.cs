namespace Vantah.App.Localization;

/// <summary>
/// Имена ресурсных строк из <c>Strings.resx</c>. Константы вместо «магических строк»:
/// опечатка в ключе ловится компилятором, а не отладкой на живом UI.
/// Значения совпадают с именами <c>data</c>-элементов в обоих .resx.
/// </summary>
public static class LocKeys
{
    // Вкладки главного окна
    public const string Tab_Status = "Tab_Status";
    public const string Tab_Locations = "Tab_Locations";
    public const string Tab_Domains = "Tab_Domains";
    public const string Tab_License = "Tab_License";
    public const string Tab_About = "Tab_About";
    public const string Tab_Processes = "Tab_Processes";

    // Общие подписи кнопок и состояний
    public const string Common_Connect = "Common_Connect";
    public const string Common_Disconnect = "Common_Disconnect";
    public const string Common_Add = "Common_Add";
    public const string Common_Clear = "Common_Clear";
    public const string Common_Cancel = "Common_Cancel";
    public const string Common_Refresh = "Common_Refresh";
    public const string Common_Import = "Common_Import";
    public const string Common_Export = "Common_Export";
    public const string Common_Loading = "Common_Loading";
    public const string Common_NotAvailable = "Common_NotAvailable";

    // Вкладка «Статус»
    public const string Status_Connected = "Status_Connected";
    public const string Status_Connecting = "Status_Connecting";
    public const string Status_Disconnecting = "Status_Disconnecting";
    public const string Status_Disconnected = "Status_Disconnected";
    public const string Status_Error = "Status_Error";
    public const string Status_SpeedLabel = "Status_SpeedLabel";
    public const string Status_SessionLabel = "Status_SessionLabel";
    public const string Status_ModeFormat = "Status_ModeFormat";
    public const string Status_Exclusions_General = "Status_Exclusions_General";
    public const string Status_Exclusions_Selective = "Status_Exclusions_Selective";
    public const string Status_SubTab_History = "Status_SubTab_History";
    public const string Status_SubTab_Log = "Status_SubTab_Log";
    public const string Status_HistoryEmpty = "Status_HistoryEmpty";
    public const string Status_HistoryEntryFormat = "Status_HistoryEntryFormat";

    // Вкладка «Локации»
    public const string Locations_SearchPlaceholder = "Locations_SearchPlaceholder";
    public const string Locations_ConnectedBadge = "Locations_ConnectedBadge";
    public const string Locations_Col_Iso = "Locations_Col_Iso";
    public const string Locations_Col_City = "Locations_Col_City";
    public const string Locations_Col_Country = "Locations_Col_Country";
    public const string Locations_Col_Ping = "Locations_Col_Ping";
    public const string Locations_Tooltip_FavoritesFirst = "Locations_Tooltip_FavoritesFirst";
    public const string Locations_Tooltip_SortByIso = "Locations_Tooltip_SortByIso";
    public const string Locations_Tooltip_SortByCity = "Locations_Tooltip_SortByCity";
    public const string Locations_Tooltip_SortByCountry = "Locations_Tooltip_SortByCountry";
    public const string Locations_Tooltip_SortByPing = "Locations_Tooltip_SortByPing";

    // Вкладка «Домены»
    public const string Domains_Mode_General = "Domains_Mode_General";
    public const string Domains_Mode_Selective = "Domains_Mode_Selective";
    public const string Domains_QueryPlaceholder = "Domains_QueryPlaceholder";
    public const string Domains_PasteFromClipboard = "Domains_PasteFromClipboard";
    public const string Domains_ExportDialogTitle = "Domains_ExportDialogTitle";
    public const string Domains_ImportDialogTitle = "Domains_ImportDialogTitle";
    public const string Domains_ClearConfirmTitle = "Domains_ClearConfirmTitle";
    public const string Domains_ClearConfirmMessage = "Domains_ClearConfirmMessage";

    // Вкладка «Лицензия»
    public const string License_Account = "License_Account";
    public const string License_Plan = "License_Plan";
    public const string License_Devices = "License_Devices";
    public const string License_Renewal = "License_Renewal";
    public const string License_Loading = "License_Loading";
    public const string License_ErrorNotLoggedIn = "License_ErrorNotLoggedIn";
    public const string License_ErrorFormat = "License_ErrorFormat";

    // Вкладка «О программе»
    public const string About_Version = "About_Version";
    public const string About_CliVersion = "About_CliVersion";
    public const string About_LegalNotice = "About_LegalNotice";

    // Вкладка «Процессы»
    public const string Processes_Col_Pid = "Processes_Col_Pid";
    public const string Processes_Col_Command = "Processes_Col_Command";
    public const string Processes_Col_Started = "Processes_Col_Started";
    public const string Processes_Kill = "Processes_Kill";
    public const string Processes_KillAll = "Processes_KillAll";
    public const string Processes_KillConfirm = "Processes_KillConfirm";
    public const string Processes_KillAllConfirm = "Processes_KillAllConfirm";
    public const string Processes_KillConfirmAction = "Processes_KillConfirmAction";
    public const string Processes_Empty = "Processes_Empty";

    // Меню и подсказка в трее
    public const string Tray_Fastest = "Tray_Fastest";
    public const string Tray_Location = "Tray_Location";
    public const string Tray_ShowWindow = "Tray_ShowWindow";
    public const string Tray_Exit = "Tray_Exit";
    public const string Tray_NoFavorites = "Tray_NoFavorites";
    public const string Tray_DomainsFormat = "Tray_DomainsFormat";
    public const string Tray_Tooltip_ConnectedFormat = "Tray_Tooltip_ConnectedFormat";
    public const string Tray_Tooltip_DisconnectedFormat = "Tray_Tooltip_DisconnectedFormat";
    public const string Tray_Tooltip_TrafficFormat = "Tray_Tooltip_TrafficFormat";
    public const string Tray_Tooltip_DomainsFormat = "Tray_Tooltip_DomainsFormat";
    public const string Tray_Tooltip_ErrorFormat = "Tray_Tooltip_ErrorFormat";
    public const string Tray_StatusConnectedFormat = "Tray_StatusConnectedFormat";

    // Выбор языка
    public const string Lang_Label = "Lang_Label";
}
