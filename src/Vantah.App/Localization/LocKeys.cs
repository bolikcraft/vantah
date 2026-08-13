namespace Vantah.App.Localization;

/// <summary>
/// Имена ресурсных строк из нейтрального (английского) <c>Strings.resx</c>. Константы вместо
/// «магических строк»: опечатка в ключе ловится компилятором, а не отладкой на живом UI.
/// Значения совпадают с именами <c>data</c>-элементов во всех .resx — нейтральном и языковых.
/// </summary>
public static class LocKeys
{
    // Вкладки главного окна
    public const string Tab_Status = "Tab_Status";
    public const string Tab_Locations = "Tab_Locations";
    public const string Tab_Domains = "Tab_Domains";

    // Меню «☰» в правом углу полосы вкладок
    public const string Menu_Tooltip = "Menu_Tooltip";
    public const string Menu_Processes = "Menu_Processes";
    public const string Menu_Settings = "Menu_Settings";
    public const string Menu_License = "Menu_License";
    public const string Menu_About = "Menu_About";

    // Общие подписи кнопок и состояний
    public const string Common_Connect = "Common_Connect";
    public const string Common_Disconnect = "Common_Disconnect";
    public const string Common_StopConnecting = "Common_StopConnecting";
    public const string Common_Add = "Common_Add";
    public const string Common_Clear = "Common_Clear";
    public const string Common_Cancel = "Common_Cancel";
    public const string Common_Refresh = "Common_Refresh";
    public const string Common_Import = "Common_Import";
    public const string Common_Export = "Common_Export";
    public const string Common_Loading = "Common_Loading";
    public const string Common_NotAvailable = "Common_NotAvailable";
    public const string Common_Apply = "Common_Apply";
    public const string Common_Reset = "Common_Reset";
    public const string Common_Saved = "Common_Saved";

    // Вкладка «Статус»
    public const string Status_Connected = "Status_Connected";
    public const string Status_Connecting = "Status_Connecting";
    public const string Status_Disconnecting = "Status_Disconnecting";
    public const string Status_Disconnected = "Status_Disconnected";
    public const string Status_Error = "Status_Error";
    public const string Status_Checking = "Status_Checking";
    public const string Status_SpeedLabel = "Status_SpeedLabel";
    public const string Status_SessionLabel = "Status_SessionLabel";
    public const string Status_ModeFormat = "Status_ModeFormat";
    public const string Status_Exclusions_General = "Status_Exclusions_General";
    public const string Status_Exclusions_Selective = "Status_Exclusions_Selective";
    public const string Status_SubTab_History = "Status_SubTab_History";
    public const string Status_SubTab_Log = "Status_SubTab_Log";
    public const string Status_HistoryEmpty = "Status_HistoryEmpty";
    public const string Status_HistoryEntryFormat = "Status_HistoryEntryFormat";
    public const string Status_DurationHm = "Status_DurationHm";
    public const string Status_DurationMinutes = "Status_DurationMinutes";
    public const string Status_IpVersion = "Status_IpVersion";

    // Вкладка «Локации»
    public const string Locations_SearchPlaceholder = "Locations_SearchPlaceholder";
    public const string Locations_Loading = "Locations_Loading";
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
    public const string Domains_InvalidEntry = "Domains_InvalidEntry";

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

    // Вкладка «Настройки»
    public const string Settings_ConnectedWarning = "Settings_ConnectedWarning";
    public const string Settings_Mode = "Settings_Mode";
    public const string Settings_SocksSection = "Settings_SocksSection";
    public const string Settings_SocksPort = "Settings_SocksPort";
    public const string Settings_SocksHost = "Settings_SocksHost";
    public const string Settings_SocksUsername = "Settings_SocksUsername";
    public const string Settings_SocksPassword = "Settings_SocksPassword";
    public const string Settings_SaveAuth = "Settings_SaveAuth";
    public const string Settings_ClearAuth = "Settings_ClearAuth";
    public const string Settings_Dns = "Settings_Dns";
    public const string Settings_DnsPlaceholder = "Settings_DnsPlaceholder";
    public const string Settings_Protocol = "Settings_Protocol";
    public const string Settings_Channel = "Settings_Channel";
    public const string Settings_Routing = "Settings_Routing";
    public const string Settings_CreateRouteScript = "Settings_CreateRouteScript";
    public const string Settings_ChangeSystemDns = "Settings_ChangeSystemDns";
    public const string Settings_PostQuantum = "Settings_PostQuantum";
    public const string Settings_ShowNotifications = "Settings_ShowNotifications";
    public const string Settings_DebugLogging = "Settings_DebugLogging";
    public const string Settings_CrashReporting = "Settings_CrashReporting";
    public const string Settings_Telemetry = "Settings_Telemetry";
    public const string Settings_ShowHints = "Settings_ShowHints";
    public const string Settings_InterfaceOverride = "Settings_InterfaceOverride";
    public const string Settings_InterfacePlaceholder = "Settings_InterfacePlaceholder";
    public const string Settings_ErrorInvalidPort = "Settings_ErrorInvalidPort";
    public const string Settings_ErrorEmptyHost = "Settings_ErrorEmptyHost";
    public const string Settings_UpdateAvailable = "Settings_UpdateAvailable";
    public const string Settings_InstallUpdate = "Settings_InstallUpdate";
    public const string Settings_UpdateDone = "Settings_UpdateDone";
    public const string Settings_UpdateUpToDate = "Settings_UpdateUpToDate";
    public const string Settings_ExportLogs = "Settings_ExportLogs";
    public const string Settings_LogsExported = "Settings_LogsExported";
    public const string Settings_StartupSection = "Settings_StartupSection";
    public const string Settings_AutoConnect = "Settings_AutoConnect";
    public const string Settings_AutoConnectLast = "Settings_AutoConnectLast";
    public const string Settings_AutoConnectFastest = "Settings_AutoConnectFastest";
    public const string Settings_Autostart = "Settings_Autostart";
    public const string Settings_CheckAppUpdates = "Settings_CheckAppUpdates";
    public const string Settings_KillSwitch = "Settings_KillSwitch";
    public const string Settings_KillSwitchHelp = "Settings_KillSwitchHelp";

    // Плашка «доступна новая версия Vantah» в главном окне
    public const string Update_Available = "Update_Available";
    public const string Update_Download = "Update_Download";
    public const string Update_Dismiss = "Update_Dismiss";

    // Выбор языка
    public const string Lang_Label = "Lang_Label";

    // Экран входа (device-code через браузер)
    public const string Login_Title = "Login_Title";
    public const string Login_Logout = "Login_Logout";
    public const string Login_LogoutConfirmTitle = "Login_LogoutConfirmTitle";
    public const string Login_LogoutConfirmMessage = "Login_LogoutConfirmMessage";
    public const string Login_LogoutConfirmAction = "Login_LogoutConfirmAction";
    public const string Login_Intro = "Login_Intro";
    public const string Login_Start = "Login_Start";
    public const string Login_OpenBrowser = "Login_OpenBrowser";
    public const string Login_Cancel = "Login_Cancel";
    public const string Login_Waiting = "Login_Waiting";
    public const string Login_CodeLabel = "Login_CodeLabel";
    public const string Login_UrlLabel = "Login_UrlLabel";
    public const string Login_CopyUrl = "Login_CopyUrl";

    // Сообщения об ошибках (коды AppErrorCode из ядра) и итог входа
    public const string Settings_LogsExportedFormat = "Settings_LogsExportedFormat";
    public const string Settings_UpdateAvailableFormat = "Settings_UpdateAvailableFormat";

    public const string Error_CommandFailed = "Error_CommandFailed";
    public const string Error_Timeout = "Error_Timeout";
    public const string Error_AddDomainFailed = "Error_AddDomainFailed";
    public const string Error_RemoveDomainFailed = "Error_RemoveDomainFailed";
    public const string Error_ModeSwitchFailed = "Error_ModeSwitchFailed";
    public const string Error_Cancelled = "Error_Cancelled";
    public const string Login_Succeeded = "Login_Succeeded";
    public const string Login_Failed = "Login_Failed";
    public const string Login_Cancelled = "Login_Cancelled";
    public const string Login_TimedOut = "Login_TimedOut";
}
