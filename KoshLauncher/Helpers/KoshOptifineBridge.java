import java.io.File;
import java.lang.reflect.InvocationTargetException;

/** Calls the installer supplied by the user/downloaded from optifine.net.
 * Compile with javac --release 8 KoshOptifineBridge.java. No OptiFine code is bundled.
 */
public final class KoshOptifineBridge {
    public static void main(String[] args) throws Exception {
        if (args.length != 1) throw new IllegalArgumentException("Expected installation directory");
        try {
            Class<?> installer = Class.forName("optifine.Installer");
            installer.getMethod("doInstall", File.class).invoke(null, new File(args[0]));
        } catch (InvocationTargetException ex) {
            ex.getCause().printStackTrace();
            System.exit(1);
        }
    }
}
