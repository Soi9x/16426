package cmd

import (
	"net"

	commonCmd "github.com/luskaner/ageLANServer/common/cmd"
	"github.com/spf13/pflag"
)

var MapIP net.IP
var AddLocalCertData []byte
var GameId string

func InitSetUp(flags *pflag.FlagSet) {
	flags.IPVarP(
		&MapIP,
		"ip",
		"i",
		nil,
		"IP to resolve in local DNS server.",
	)
	flags.BytesBase64VarP(
		&AddLocalCertData,
		"localCert",
		"l",
		nil,
		"Add the certificate to the local machine's trusted root store",
	)
	commonCmd.GameVarCommand(flags, &GameId)
}
