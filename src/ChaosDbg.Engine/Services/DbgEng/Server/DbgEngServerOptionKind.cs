namespace ChaosDbg.DbgEng.Server
{
    /* There are several documents that list syntaxes you can use for launching a remote debug target
     *
     * Sources
     * -------
     * KD             : https://learn.microsoft.com/en-us/windows-hardware/drivers/debugger/performing-kernel-mode-debugging-using-kd
     * Debugger Server: https://learn.microsoft.com/en-us/windows-hardware/drivers/debugger/activating-a-debugging-server
     * dbgsrv         : https://learn.microsoft.com/en-us/windows-hardware/drivers/debugger/activating-a-process-server
     * kdsrv          : https://learn.microsoft.com/en-us/windows-hardware/drivers/debugger/activating-a-kd-connection-server
     * Debugger kdsrv : https://learn.microsoft.com/en-us/windows-hardware/drivers/debugger/activating-a-smart-client--kernel-mode-
     *
     * npipe
     * -----
     * KD             : N/A
     * Debugger Server: Debugger -server           npipe:              pipe=PipeName[,hidden][,password=Password][,IcfEnable] [-noio] [Options]
     * dbgsrv         : dbgsrv -t                  npipe:              pipe=PipeName[,hidden][,password=Password][,IcfEnable] [[-sifeo Executable] -c[s] AppCmdLine] [-x | -pc] 
     * kdsrv          : kdsrv -t                   npipe:              pipe=PipeName[,hidden][,password=Password][,IcfEnable] 
     * Debugger kdsrv : Debugger -k kdsrv:server=@{npipe:server=Server,pipe=PipeName         [,password=Password]},trans=@{ConnectType} [Options]
     *
     * tcp
     * ---
     * KD             : N/A
     * Debugger Server: Debugger -server           tcp:port=Socket[,hidden]     [,password=Password][,ipversion=6][,IcfEnable] [-noio] [Options]
     * dbgsrv         : dbgsrv -t                  tcp:port=Socket[,hidden]     [,password=Password][,ipversion=6][,IcfEnable] [[-sifeo Executable] -c[s] AppCmdLine] [-x | -pc] 
     * kdsrv          : kdsrv -t                   tcp:port=Socket[,hidden]     [,password=Password][,ipversion=6][,IcfEnable] 
     * Debugger kdsrv : Debugger -k kdsrv:server=@{tcp:server=Server,port=Socket[,password=Password][,ipversion=6]},trans=@{ConnectType} [Options]
     *
     * reverse tcp
     * -----------
     * KD             : N/A
     * Debugger Server: Debugger -server           tcp:port=Socket,clicon=Client[,password=Password][,ipversion=6] [-noio] [Options]
     * dbgsrv         : dbgsrv -t                  tcp:port=Socket,clicon=Client[,password=Password][,ipversion=6] [[-sifeo Executable] -c[s] AppCmdLine] [-x | -pc] 
     * kdsrv          : kdsrv -t                   tcp:port=Socket,clicon=Client[,password=Password][,ipversion=6] 
     * Debugger kdsrv : Debugger -k kdsrv:server=@{tcp:clicon=Server,port=Socket[,password=Password][,ipversion=6]},trans=@{ConnectType} [Options]
     *
     * com
     * ---
     * KD             : N/A
     * Debugger Server: Debugger -server           com:port=COMPort,baud=BaudRate,channel=COMChannel[,hidden][,password=Password] [-noio] [Options]
     * dbgsrv         : dbgsrv -t                  com:port=COMPort,baud=BaudRate,channel=COMChannel[,hidden][,password=Password] [[-sifeo Executable] -c[s] AppCmdLine] [-x | -pc] 
     * kdsrv          : kdsrv -t                   com:port=COMPort,baud=BaudRate,channel=COMChannel[,hidden][,password=Password] 
     * Debugger kdsrv : Debugger -k kdsrv:server=@{com:port=COMPort,baud=BaudRate,channel=COMChannel         [,password=Password]},trans=@{ConnectType} [Options]
     *
     * spipe
     * -----
     * KD             : 
     * Debugger Server: Debugger -server           spipe:proto=Protocol,{certuser=Cert|machuser=Cert}              ,pipe=PipeName[,hidden][,password=Password] [-noio] [Options]
     * dbgsrv         : dbgsrv -t                  spipe:proto=Protocol,{certuser=Cert|machuser=Cert}              ,pipe=PipeName[,hidden][,password=Password] [[-sifeo Executable] -c[s] AppCmdLine] [-x | -pc] 
     * kdsrv          : kdsrv -t                   spipe:proto=Protocol,{certuser=Cert|machuser=Cert}              ,pipe=PipeName[,hidden][,password=Password] 
     * Debugger kdsrv : Debugger -k kdsrv:server=@{spipe:proto=Protocol,{certuser=Cert|machuser=Cert},server=Server,pipe=PipeName         [,password=Password]},trans=@{ConnectType} [Options]
     *
     * ssl
     * ---
     * KD             : 
     * Debugger Server: Debugger -server           ssl:proto=Protocol,{certuser=Cert|machuser=Cert}              ,port=Socket[,hidden][,password=Password] [-noio] [Options]
     * dbgsrv         : dbgsrv -t                  ssl:proto=Protocol,{certuser=Cert|machuser=Cert}              ,port=Socket[,hidden][,password=Password] [[-sifeo Executable] -c[s] AppCmdLine] [-x | -pc] 
     * kdsrv          : kdsrv -t                   ssl:proto=Protocol,{certuser=Cert|machuser=Cert}              ,port=Socket[,hidden][,password=Password] 
     * Debugger kdsrv : Debugger -k kdsrv:server=@{ssl:proto=Protocol,{certuser=Cert|machuser=Cert},server=Server,port=Socket         [,password=Password]},trans=@{ConnectType} [Options]
     *
     * reverse ssl
     * -----------
     * KD             : 
     * Debugger Server: Debugger -server           ssl:proto=Protocol,{certuser=Cert|machuser=Cert},port=Socket,clicon=Client[,password=Password] [-noio] [Options]
     * dbgsrv         : dbgsrv -t                  ssl:proto=Protocol,{certuser=Cert|machuser=Cert},port=Socket,clicon=Client[,password=Password] [[-sifeo Executable] -c[s] AppCmdLine] [-x | -pc] 
     * kdsrv          : kdsrv -t                   ssl:proto=Protocol,{certuser=Cert|machuser=Cert},port=Socket,clicon=Client[,password=Password]
     * Debugger kdsrv : Debugger -k kdsrv:server=@{ssl:proto=Protocol,{certuser=Cert|machuser=Cert},clicon=Server,port=Socket[,password=Password]},trans=@{ConnectType} [Options]
     *
     * The KD connecton options are slightly different:
     *
     * KD: https://learn.microsoft.com/en-us/windows-hardware/drivers/debugger/performing-kernel-mode-debugging-using-kd
     *
     *     kd [-y SymbolPath] -k net:port=PortNumber,key=Key[,target=TargetIPAddress|TargetHostName]
     *     kd [-y SymbolPath] -k 1394:channel=1394Channel[,symlink=1394Protocol]
     *     kd [-y SymbolPath] -k usb:targetname=USBString
     *     kd [-y SymbolPath] -k com:port=ComPort,baud=BaudRate
     *     kd [-y SymbolPath] -k com:ipport=SerialTcpIpPort,port=SerialIPAddress
     *     kd [-y SymbolPath] -k com:pipe,port=\\VMHost\pipe\PipeName[,resets=0][,reconnect]
     *     kd [-y SymbolPath] -k com:modem */

    public enum DbgEngServerOptionKind
    {
        Unknown,

        //Parameters
        Pipe,
        Password,
        Port,
        IPVersion,
        CliCon,
        Baud,
        Channel,
        Proto,
        CertUser,
        MachUser,

        //Switches
        Hidden,
        IcfEnable,

        //Client Only
        Server
    }
}
