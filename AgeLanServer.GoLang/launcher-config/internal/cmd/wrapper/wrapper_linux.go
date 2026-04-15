package wrapper

import "crypto/x509"

func RemoveUserCerts() (crts []*x509.Certificate, err error) {
	// Must not be called
	return nil, nil
}

func AddUserCerts(_ any) error {
	// Must not be called
	return nil
}
